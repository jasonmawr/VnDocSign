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
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Infrastructure.Persistence;
using System;
using System.Threading;

namespace VnDocSign.Infrastructure.Services;

public sealed class SigningService : ISigningService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _dossierLocks = new();

    private readonly AppDbContext _db;
    private readonly ISignActivationService _activation;
    private readonly IPdfRenderService _pdf;
    private readonly ISsmClient _ssm;
    private readonly IConfiguration _config;
    private readonly ILogger<SigningService> _logger;

    public SigningService(
        AppDbContext db,
        ISignActivationService activation,
        IPdfRenderService pdf,
        ISsmClient ssm,
        IConfiguration config,
        ILogger<SigningService> logger)
    {
        _db = db;
        _activation = activation;
        _pdf = pdf;
        _ssm = ssm;
        _config = config;
        _logger = logger;
    }

    // ========= Helpers for file strategy =========
    private string GetRoot() => _config["FileStorage:Root"] ?? "./data";
    private string GetDossierFolder(Guid dossierId) => Path.Combine(GetRoot(), "dossiers", dossierId.ToString("N"));
    private string GetSourcePdfPath(Guid dossierId) => Path.Combine(GetDossierFolder(dossierId), "Source.pdf");
    private string GetSignedPath(Guid dossierId, int version) => Path.Combine(GetDossierFolder(dossierId), $"Signed_v{version}.pdf");
    private string GetCurrentPointerPath(Guid dossierId) => Path.Combine(GetDossierFolder(dossierId), "current.pointer");

    private async Task<string?> ReadCurrentSignedAsync(Guid dossierId, CancellationToken ct)
    {
        var p = GetCurrentPointerPath(dossierId);
        if (!File.Exists(p)) return null;
        return await File.ReadAllTextAsync(p, ct);
    }

    private async Task WriteCurrentSignedAsync(Guid dossierId, string path, CancellationToken ct)
    {
        Directory.CreateDirectory(GetDossierFolder(dossierId));
        await File.WriteAllTextAsync(GetCurrentPointerPath(dossierId), path, ct);
    }

    private static SemaphoreSlim GetDossierLock(Guid dossierId)
        => _dossierLocks.GetOrAdd(dossierId, _ => new SemaphoreSlim(1, 1));

    // ========= Business Methods =========

    public async Task ApproveAndSignAsync(ApproveRequest req, CancellationToken ct = default)
    {
        // 0) Load & quyền
        var t = await _db.SignTasks
            .Include(x => x.Dossier)
            .FirstOrDefaultAsync(x => x.Id == req.TaskId, ct)
            ?? throw new InvalidOperationException("Task not found.");

        if (!t.IsActivated || t.Status != SignTaskStatus.Pending)
            throw new InvalidOperationException("Task not ready or already processed.");

        if (t.AssigneeId != req.ActorUserId)
            throw new UnauthorizedAccessException("User not allowed.");

        var dossier = t.Dossier ?? throw new InvalidOperationException("Dossier not found.");

        // 1) Lấy DigitalIdentity đang active của actor
        var di = await _db.Set<DigitalIdentity>()
            .Where(x => x.UserId == req.ActorUserId && x.IsActive)
            .FirstOrDefaultAsync(ct);

        if (di is null)
            throw new InvalidOperationException("Tài khoản chưa có DigitalIdentity đang hoạt động. Liên hệ Admin để cấu hình.");

        // 2) Lock theo Dossier (tránh race khi nhóm song song cùng ghi file)
        var locker = GetDossierLock(dossier.Id);
        await locker.WaitAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 3) Lấy InputPdf: nếu chưa có bản ký -> render source; nếu có -> dùng bản current
            string? current = await ReadCurrentSignedAsync(dossier.Id, ct);
            string inputPdf;
            if (string.IsNullOrWhiteSpace(current) || !File.Exists(current))
            {
                inputPdf = await _pdf.RenderDossierToPdf(dossier.Id, ct);
                if (!inputPdf.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    inputPdf = await _pdf.EnsurePdf(inputPdf, ct);
            }
            else
            {
                inputPdf = current;
            }

            // 4) Chuẩn bị OutputPdf (tăng version) – dùng cho SSM
            var folder = GetDossierFolder(dossier.Id);
            Directory.CreateDirectory(folder);
            var version = 1;
            while (File.Exists(GetSignedPath(dossier.Id, version))) version++;
            var outputPdf = GetSignedPath(dossier.Id, version);

            // 5) (Stub) chèn GHICHU_* nếu có comment
            if (!string.IsNullOrWhiteSpace(req.Comment))
            {
                _logger.LogInformation("GHICHU_* (stub) - Comment: {Comment}", req.Comment);
            }

            // Chuẩn bị biến chung cho SignEvent / pointer
            string finalPdfPath;
            SignEvent ev;

            // 6) Nếu Pin rỗng => Ký MOCK PNG, không gọi SSM (Phase 1: mock ký + lưu event)
            if (string.IsNullOrWhiteSpace(req.Pin))
            {
                // Đánh dấu Task hiện tại đã Approved TRƯỚC khi render mock,
                // để PdfRenderService nhìn thấy và dán PNG chữ ký cho chính slot này.
                t.Status = SignTaskStatus.Approved;
                t.DecidedAt = DateTime.UtcNow;
                t.Comment = req.Comment;

                // Ký mock: PdfRenderService sẽ
                // - copy Source.docx → Signed_vN.docx
                // - chèn PNG vào các alias SIG_* những slot đã Approved
                // - convert ra Signed_vN.pdf và trả về path
                var mockPdf = await _pdf.RenderSignedMockAsync(dossier.Id, ct);

                finalPdfPath = mockPdf;

                ev = new SignEvent
                {
                    DossierId = dossier.Id,
                    ActorUserId = req.ActorUserId,
                    PdfPathIn = inputPdf,
                    PdfPathOut = mockPdf,
                    // 0 = Mock, 1 = SSM (giữ đúng ý nghĩa SignType = Mock / SSM)
                    SignType = 0,
                    SignLocationType = 0,
                    SearchPattern = null,
                    Page = null,
                    PositionX = null,
                    PositionY = null,
                    VisibleSignatureName = t.VisiblePattern,
                    Success = true,
                    Error = null,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.Set<SignEvent>().Add(ev);
            }
            else
            {
                // 6b) Ký số thật qua SSM
                //    Mặc định ký theo SearchPattern nếu có (page=1 nếu không cấu hình).
                //    Theo tài liệu: SignLocationType = 2 cho SearchPattern, 1 cho toạ độ X/Y.
                string? searchPattern = t.VisiblePattern;
                int signLocationType;
                int? page = null; // để null nếu backend SSM tự tìm trang theo pattern; nếu cần, đặt mặc định 1.
                float? posX = null;
                float? posY = null;

                if (!string.IsNullOrWhiteSpace(searchPattern))
                {
                    // 2 = SearchPattern (đúng tài liệu BE+FE)
                    signLocationType = 2;
                    if (page is null) page = 1; // page mặc định an toàn khi ký pattern
                }
                else
                {
                    // Ở codebase hiện tại chưa có toạ độ trong SignTask.
                    // Nếu muốn fallback toạ độ, cần lấy từ SignSlotDef (khác bảng) – chưa triển khai ở giai đoạn này.
                    throw new InvalidOperationException("Không có VisiblePattern cho slot hiện tại và chưa cấu hình toạ độ fallback.");
                }

                // 7) Gọi SSM thật
                var signReq = new SignPdfRequest(
                    EmpCode: di.EmpCode,
                    Pin: req.Pin ?? string.Empty,
                    CertName: di.CertName,
                    Company: di.Company,
                    Title: di.Title ?? string.Empty,
                    Name: di.DisplayName ?? string.Empty,
                    InputPdfPath: inputPdf,
                    OutputPdfPath: outputPdf,
                    SignType: 1,                      // 1 = ký hình ảnh (SSM)
                    SignLocationType: signLocationType,
                    SearchPattern: searchPattern,
                    Page: page,
                    PositionX: posX,
                    PositionY: posY,
                    BearerToken: null                 // có thể truyền token SSM nếu cần (_config["Ssm:StaticBearer"])
                );

                var result = await _ssm.SignPdfAsync(signReq, ct);

                // 8) Ghi SignEvent
                ev = new SignEvent
                {
                    DossierId = dossier.Id,
                    ActorUserId = req.ActorUserId,
                    PdfPathIn = inputPdf,
                    PdfPathOut = outputPdf,
                    SignType = signReq.SignType,
                    SignLocationType = signReq.SignLocationType,
                    SearchPattern = signReq.SearchPattern,
                    Page = signReq.Page,
                    PositionX = signReq.PositionX,
                    PositionY = signReq.PositionY,
                    VisibleSignatureName = t.VisiblePattern,
                    Success = result.Success,
                    Error = result.Error,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.Set<SignEvent>().Add(ev);

                if (!result.Success)
                {
                    await _db.SaveChangesAsync(ct); // lưu event lỗi
                    await tx.RollbackAsync(ct);
                    throw new InvalidOperationException("Ký số thất bại: " + (result.Error ?? "unknown"));
                }

                finalPdfPath = outputPdf;
            }

            // 9) Cập nhật pointer current (dùng chung cho cả mock + SSM)
            await WriteCurrentSignedAsync(dossier.Id, finalPdfPath, ct);

            // 10) Cập nhật trạng thái task & dossier
            t.Status = SignTaskStatus.Approved;
            t.DecidedAt = DateTime.UtcNow;
            t.Comment = req.Comment;

            var anyPending = await _db.SignTasks
                .AnyAsync(x => x.DossierId == t.DossierId && x.Status == SignTaskStatus.Pending, ct);

            t.Dossier!.Status = anyPending ? DossierStatus.InProgress : DossierStatus.Approved;

            await _db.SaveChangesAsync(ct);

            // 11) Mở bước kế (SignActivationService)
            await _activation.RecomputeAsync(t.DossierId, ct);

            await tx.CommitAsync(ct);
        }
        finally
        {
            locker.Release();
        }
    }

    public async Task RejectAsync(RejectRequest req, CancellationToken ct = default)
    {
        var t = await _db.SignTasks.Include(x => x.Dossier).FirstOrDefaultAsync(x => x.Id == req.TaskId, ct)
            ?? throw new InvalidOperationException("Task not found.");
        if (!t.IsActivated || t.Status != SignTaskStatus.Pending) throw new InvalidOperationException("Not ready.");
        if (t.AssigneeId != req.ActorUserId) throw new UnauthorizedAccessException();

        t.Status = SignTaskStatus.Rejected;
        t.DecidedAt = DateTime.UtcNow;
        t.Comment = req.Comment;
        t.Dossier!.Status = DossierStatus.Rejected;

        await _db.SaveChangesAsync(ct);
        await _activation.RecomputeAsync(t.DossierId, ct);
    }

    public async Task ClerkConfirmAsync(ClerkConfirmRequest req, CancellationToken ct = default)
    {
        var vt = await _db.SignTasks.FirstOrDefaultAsync(t => t.DossierId == req.DossierId && t.SlotKey == SlotKey.VanThuCheck, ct)
            ?? throw new InvalidOperationException("Clerk slot not found.");
        if (!vt.IsActivated) throw new InvalidOperationException("Not clerk step yet.");

        // *Lưu ý*: ở PHASE 1 bạn chưa check vt.AssigneeId == req.ActorUserId.
        // Nếu muốn chặt chẽ, mở khoá dòng dưới:
        // if (vt.AssigneeId != req.ActorUserId) throw new UnauthorizedAccessException();

        vt.ClerkConfirmed = true;
        vt.Status = SignTaskStatus.Approved;
        vt.DecidedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _activation.RecomputeAsync(req.DossierId, ct);
    }

    public async Task<IReadOnlyList<MyTaskItem>> GetMyTasksAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.SignTasks.AsNoTracking()
            .Where(t => t.AssigneeId == userId && t.IsActivated && t.Status == SignTaskStatus.Pending)
            .Select(t => new MyTaskItem(t.Id, t.DossierId, t.SlotKey, t.Order))
            .ToListAsync(ct);
    }
}
