using System.Collections.Concurrent;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VnDocSign.Application.Contracts.Dtos.Signing;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Application.Contracts.Interfaces.Integration;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Infrastructure.Services
{
    public sealed class SigningService : ISigningService
    {
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

        private readonly AppDbContext _db;
        private readonly ISignActivationService _activation;
        private readonly IPdfRenderService _pdf;
        private readonly ISsmClient _ssm;
        private readonly IConfiguration _config;
        private readonly ILogger<SigningService> _logger;
        private readonly IFileVersioningService _fileVersioning;

        public SigningService(
            AppDbContext db,
            ISignActivationService activation,
            IPdfRenderService pdf,
            ISsmClient ssm,
            IConfiguration config,
            ILogger<SigningService> logger,
            IFileVersioningService fileVersioning)
        {
            _db = db;
            _activation = activation;
            _pdf = pdf;
            _ssm = ssm;
            _config = config;
            _logger = logger;
            _fileVersioning = fileVersioning;
        }

        private static SemaphoreSlim GetLock(Guid id)
            => _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        // ==========================================================
        // Helper: kiểm tra actor có quyền thao tác trên task (bao gồm ủy quyền)
        // ==========================================================
        private async Task<bool> IsActorAllowedForTaskAsync(SignTask task, Guid actorUserId, CancellationToken ct)
        {
            if (task.AssigneeId == actorUserId)
                return true;

            var now = DateTime.UtcNow;

            return await _db.UserDelegations.AsNoTracking()
                .AnyAsync(d =>
                    d.IsActive &&
                    d.FromUserId == task.AssigneeId &&
                    d.ToUserId == actorUserId &&
                    d.StartUtc <= now &&
                    (d.EndUtc == null || d.EndUtc >= now),
                    ct);
        }

        // ==========================================================
        // APPROVE + SIGN (MOCK hoặc SSM thật tuỳ Mode + Pin)
        // ==========================================================
        public async Task ApproveAndSignAsync(ApproveRequest req, CancellationToken ct = default)
        {
            // Load task
            var t = await _db.SignTasks
                .Include(x => x.Dossier)
                .FirstOrDefaultAsync(x => x.Id == req.TaskId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy task.");

            if (!t.IsActivated || t.Status != SignTaskStatus.Pending)
                throw new InvalidOperationException("Bước ký này chưa được kích hoạt.");

            if (!await IsActorAllowedForTaskAsync(t, req.ActorUserId, ct))
                throw new UnauthorizedAccessException("Bạn không được phép ký bước này.");

            var dossier = t.Dossier!;

            // Mode ký
            var mode = _config["Ssm:Mode"] ?? "Real";
            bool isMockMode = mode.Equals("Mock", StringComparison.OrdinalIgnoreCase);
            bool useSsm = !isMockMode && !string.IsNullOrWhiteSpace(req.Pin);

            // Nếu ký thật → phải có DigitalIdentity
            DigitalIdentity? di = null;
            if (useSsm)
            {
                di = await _db.DigitalIdentities
                    .FirstOrDefaultAsync(x => x.UserId == req.ActorUserId && x.IsActive, ct)
                    ?? throw new InvalidOperationException("Bạn chưa có chứng thư số đang hoạt động.");
            }

            // Lock theo dossier
            var locker = GetLock(dossier.Id);
            await locker.WaitAsync(ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Input PDF (nếu chưa có file ký → render mới)
                string? current = await _fileVersioning.GetCurrentPointerAsync(dossier.Id, ct);
                string inputPdf = !string.IsNullOrWhiteSpace(current) && File.Exists(current)
                    ? current
                    : await _pdf.RenderDossierToPdf(dossier.Id, ct);

                if (!inputPdf.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    inputPdf = await _pdf.EnsurePdf(inputPdf, ct);

                SignEvent ev;
                string finalSignedPath;

                if (!useSsm)
                {
                    // ===============================
                    // MOCK SIGN
                    // ===============================
                    t.Status = SignTaskStatus.Approved;
                    t.DecidedAt = DateTime.UtcNow;
                    t.Comment = req.Comment;

                    var mockTemp = await _pdf.RenderSignedMockAsync(dossier.Id, ct);
                    finalSignedPath = await _fileVersioning.SaveSignedVersionAsync(dossier.Id, mockTemp, ct);

                    ev = new SignEvent
                    {
                        DossierId = dossier.Id,
                        ActorUserId = req.ActorUserId,
                        PdfPathIn = inputPdf,
                        PdfPathOut = finalSignedPath,
                        SignType = 0,
                        SignLocationType = 0,
                        VisibleSignatureName = t.VisiblePattern,
                        Success = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _db.SignEvents.Add(ev);
                }
                else
                {
                    // ===============================
                    // REAL SSM SIGN
                    // ===============================

                    if (string.IsNullOrWhiteSpace(t.VisiblePattern))
                        throw new InvalidOperationException("Slot hiện tại chưa được cấu hình VisiblePattern.");

                    // File tạm, FileVersioningService sẽ move sang Signed_vN + cập nhật pointer
                    var tempPath = Path.GetTempFileName();
                    var tempPdfOut = Path.ChangeExtension(tempPath, ".pdf");

                    var signReq = new SignPdfRequest(
                        EmpCode: di!.EmpCode,
                        Pin: req.Pin!,
                        CertName: di.CertName,
                        Company: di.Company,
                        Title: di.Title ?? "",
                        Name: di.DisplayName ?? "",
                        InputPdfPath: inputPdf,
                        OutputPdfPath: tempPdfOut,
                        SignType: 1,
                        SignLocationType: 2,
                        SearchPattern: t.VisiblePattern,
                        Page: 1,
                        PositionX: null,
                        PositionY: null,
                        BearerToken: null
                    );

                    var result = await _ssm.SignPdfAsync(signReq, ct);
                    if (!result.Success)
                    {
                        _logger.LogWarning(
                            "Ký SSM thất bại Task={TaskId} Dossier={DossierId} User={UserId} Error={Error}",
                            t.Id, dossier.Id, req.ActorUserId, result.Error);

                        await tx.RollbackAsync(ct);
                        throw new InvalidOperationException(
                            $"Ký số thất bại. Vui lòng kiểm tra chứng thư số hoặc thử lại. (Chi tiết: {result.Error})");
                    }

                    finalSignedPath = await _fileVersioning.SaveSignedVersionAsync(dossier.Id, tempPdfOut, ct);

                    ev = new SignEvent
                    {
                        DossierId = dossier.Id,
                        ActorUserId = req.ActorUserId,
                        PdfPathIn = inputPdf,
                        PdfPathOut = finalSignedPath,
                        SignType = 1,
                        SignLocationType = 2,
                        SearchPattern = t.VisiblePattern,
                        Success = true,
                        Error = result.Error,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _db.SignEvents.Add(ev);
                }

                // Cập nhật trạng thái hồ sơ
                bool pending = await _db.SignTasks
                    .AnyAsync(x => x.DossierId == dossier.Id && x.Status == SignTaskStatus.Pending, ct);

                dossier.Status = pending ? DossierStatus.InProgress : DossierStatus.Approved;

                await _db.SaveChangesAsync(ct);

                // Kích hoạt bước kế tiếp
                await _activation.RecomputeAsync(dossier.Id, ct);

                await tx.CommitAsync(ct);
            }
            finally
            {
                locker.Release();
            }
        }

        // ==========================================================
        // REJECT
        // ==========================================================
        public async Task RejectAsync(RejectRequest req, CancellationToken ct = default)
        {
            var t = await _db.SignTasks
                .Include(x => x.Dossier)
                .FirstOrDefaultAsync(x => x.Id == req.TaskId, ct)
                ?? throw new InvalidOperationException("Không tìm thấy task.");

            if (!t.IsActivated || t.Status != SignTaskStatus.Pending)
                throw new InvalidOperationException("Bước ký này chưa thể từ chối.");

            if (!await IsActorAllowedForTaskAsync(t, req.ActorUserId, ct))
                throw new UnauthorizedAccessException("Bạn không được phép từ chối bước này.");

            t.Status = SignTaskStatus.Rejected;
            t.DecidedAt = DateTime.UtcNow;
            t.Comment = req.Comment;

            t.Dossier!.Status = DossierStatus.Rejected;

            await _db.SaveChangesAsync(ct);
            await _activation.RecomputeAsync(t.DossierId, ct);
        }

        // ==========================================================
        // CLERK CONFIRM
        // ==========================================================
        public async Task ClerkConfirmAsync(ClerkConfirmRequest req, CancellationToken ct = default)
        {
            var vt = await _db.SignTasks
                .Include(t => t.Dossier)
                .FirstOrDefaultAsync(t => t.DossierId == req.DossierId && t.SlotKey == SlotKey.VanThuCheck, ct)
                ?? throw new InvalidOperationException("Không tìm thấy bước Văn thư.");

            if (!vt.IsActivated)
                throw new InvalidOperationException("Chưa đến bước Văn thư.");

            if (!await IsActorAllowedForTaskAsync(vt, req.ActorUserId, ct))
                throw new UnauthorizedAccessException("Bạn không phải người phụ trách bước này.");

            vt.ClerkConfirmed = true;
            vt.Status = SignTaskStatus.Approved;
            vt.DecidedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            await _activation.RecomputeAsync(req.DossierId, ct);
        }

        // ==========================================================
        // GET MY TASKS (simple list – backward compatible)
        // ==========================================================
        public async Task<IReadOnlyList<MyTaskItem>> GetMyTasksAsync(Guid userId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // Task trực tiếp
            var direct = _db.SignTasks.AsNoTracking()
                .Where(t => t.AssigneeId == userId && t.IsActivated && t.Status == SignTaskStatus.Pending);

            // Task được ủy quyền
            var delegated =
                from t in _db.SignTasks.AsNoTracking()
                join d in _db.UserDelegations.AsNoTracking()
                    on t.AssigneeId equals d.FromUserId
                where d.ToUserId == userId
                      && d.IsActive
                      && d.StartUtc <= now
                      && (d.EndUtc == null || d.EndUtc >= now)
                      && t.IsActivated
                      && t.Status == SignTaskStatus.Pending
                select t;

            return await direct
                .Union(delegated)
                .Select(t => new MyTaskItem(t.Id, t.DossierId, t.SlotKey, t.Order))
                .ToListAsync(ct);
        }

        // ==========================================================
        // GET MY TASKS (GROUPED)
        // ==========================================================
        public async Task<MyTasksGroupedDto> GetMyTasksGroupedAsync(Guid userId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // ========== 1) PENDING ==========

            var pendingDirect = _db.SignTasks
                .Include(t => t.Dossier)
                .Where(t =>
                    t.AssigneeId == userId &&
                    t.IsActivated &&
                    t.Status == SignTaskStatus.Pending);

            var pendingDelegated =
                from t in _db.SignTasks.Include(x => x.Dossier)
                join d in _db.UserDelegations
                    on t.AssigneeId equals d.FromUserId
                where d.ToUserId == userId
                      && d.IsActive
                      && d.StartUtc <= now
                      && (d.EndUtc == null || d.EndUtc >= now)
                      && t.IsActivated
                      && t.Status == SignTaskStatus.Pending
                select t;

            var pendingQuery = pendingDirect.Union(pendingDelegated);

            var pending = await pendingQuery
                .AsNoTracking()
                .OrderByDescending(t => t.Dossier!.CreatedAt)
                .Select(t => new MyTaskListItem(
                    t.Id,
                    t.DossierId,
                    t.Dossier!.Code,
                    t.Dossier.Title,
                    t.Dossier.Status,
                    t.SlotKey,
                    t.Order,
                    t.Dossier.CreatedAt
                ))
                .ToListAsync(ct);

            // ========== 2) PROCESSED ==========

            var processed = await _db.SignTasks
                .AsNoTracking()
                .Include(t => t.Dossier)
                .Where(t =>
                    t.AssigneeId == userId &&
                    (t.Status == SignTaskStatus.Approved || t.Status == SignTaskStatus.Rejected))
                .OrderByDescending(t => t.DecidedAt ?? t.Dossier!.CreatedAt)
                .Select(t => new MyTaskListItem(
                    t.Id,
                    t.DossierId,
                    t.Dossier!.Code,
                    t.Dossier.Title,
                    t.Dossier.Status,
                    t.SlotKey,
                    t.Order,
                    t.Dossier.CreatedAt
                ))
                .ToListAsync(ct);

            // ========== 3) COMPLETED ==========

            var completed = await _db.SignTasks
                .AsNoTracking()
                .Include(t => t.Dossier)
                .Where(t =>
                    t.AssigneeId == userId &&
                    (t.Dossier!.Status == DossierStatus.Approved || t.Dossier.Status == DossierStatus.Rejected))
                .OrderByDescending(t => t.Dossier!.CreatedAt)
                .Select(t => new MyTaskListItem(
                    t.Id,
                    t.DossierId,
                    t.Dossier!.Code,
                    t.Dossier.Title,
                    t.Dossier.Status,
                    t.SlotKey,
                    t.Order,
                    t.Dossier.CreatedAt
                ))
                .ToListAsync(ct);

            return new MyTasksGroupedDto(pending, processed, completed);
        }
    }
}
