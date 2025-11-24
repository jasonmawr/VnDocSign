using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class DossierService : IDossierService
{
    private readonly AppDbContext _db;
    private readonly ISignActivationService _activation;

    public DossierService(AppDbContext db, ISignActivationService activation)
    {
        _db = db;
        _activation = activation;
    }

    // === GĐ2: Tạo Dossier + Vùng 1 (Người trình, LĐ phòng) ===
    public async Task<DossierCreateResponse> CreateAsync(DossierCreateRequest req, CancellationToken ct = default)
    {
        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.CreatedById, ct)
            ?? throw new InvalidOperationException("Creator not found.");

        var dossier = new Dossier
        {
            Title = req.Title,
            Code = req.Code,
            CreatedById = req.CreatedById,
            Status = DossierStatus.Draft
        };

        _db.Dossiers.Add(dossier);
        await _db.SaveChangesAsync(ct);

        int ord = 1;

        // Vùng 1: Người trình
        _db.SignTasks.Add(new SignTask
        {
            DossierId = dossier.Id,
            AssigneeId = creator.Id,
            Order = ord++,
            SlotKey = SlotKey.NguoiTrinh,
            Phase = SlotPhase.Vung1,
            Status = SignTaskStatus.Pending,
            VisiblePattern = VisiblePattern(SlotKey.NguoiTrinh)
        });

        // Vùng 1: Lãnh đạo phòng của Người trình
        var leaderCreator = await FindLeaderOfDepartment(creator.DepartmentId, ct);
        if (leaderCreator != null)
        {
            _db.SignTasks.Add(new SignTask
            {
                DossierId = dossier.Id,
                AssigneeId = leaderCreator.Value,
                Order = ord++,
                SlotKey = SlotKey.LanhDaoPhong,
                Phase = SlotPhase.Vung1,
                Status = SignTaskStatus.Pending,
                VisiblePattern = VisiblePattern(SlotKey.LanhDaoPhong)
            });
        }

        await _db.SaveChangesAsync(ct);

        dossier.Status = DossierStatus.Submitted;
        await _db.SaveChangesAsync(ct);

        // Kích hoạt slot phù hợp (thường là Người trình)
        await _activation.RecomputeAsync(dossier.Id, ct);

        return new DossierCreateResponse(dossier.Id);
    }

    // === GĐ3 + GĐ4: Cập nhật route (phòng liên quan + phòng chức năng) ===
    public async Task RouteAsync(
        Guid dossierId,
        Guid? relatedDepartmentId,
        IReadOnlyCollection<string> selectedFunctionalSlots,
        CancellationToken ct = default)
    {
        var dossier = await _db.Dossiers.FirstOrDefaultAsync(d => d.Id == dossierId, ct)
            ?? throw new InvalidOperationException("Dossier not found.");

        // Chuẩn hóa list slot chức năng
        var selected = (selectedFunctionalSlots ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Lấy order lớn nhất hiện tại để cộng dồn cho các slot mới
        var maxOrder = await _db.SignTasks
            .Where(t => t.DossierId == dossierId)
            .Select(t => (int?)t.Order)
            .MaxAsync(ct) ?? 0;

        var ord = maxOrder + 1;

        // --- Đơn vị liên quan (Vùng 1) ---
        if (relatedDepartmentId.HasValue)
        {
            var leaderRel = await FindLeaderOfDepartment(relatedDepartmentId.Value, ct);
            if (leaderRel != null)
            {
                bool exists = await _db.SignTasks
                    .AnyAsync(t => t.DossierId == dossierId && t.SlotKey == SlotKey.DonViLienQuan, ct);

                if (!exists)
                {
                    _db.SignTasks.Add(new SignTask
                    {
                        DossierId = dossierId,
                        AssigneeId = leaderRel.Value,
                        Order = ord++,
                        SlotKey = SlotKey.DonViLienQuan,
                        Phase = SlotPhase.Vung1,
                        Status = SignTaskStatus.Pending,
                        VisiblePattern = VisiblePattern(SlotKey.DonViLienQuan)
                    });
                }
            }
        }

        // --- Vùng 2: các phòng chức năng được chọn ---
        var functionalSlotKeys = new[] { SlotKey.KHTH, SlotKey.HCQT, SlotKey.TCCB, SlotKey.TCKT, SlotKey.CTCD };

        foreach (var code in selected)
        {
            if (!Enum.TryParse<SlotKey>(code, ignoreCase: true, out var slotKey))
                continue;

            if (!functionalSlotKeys.Contains(slotKey))
                continue;

            bool exists = await _db.SignTasks
                .AnyAsync(t => t.DossierId == dossierId && t.SlotKey == slotKey, ct);

            if (exists)
                continue;

            var depId = (await _db.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slot == slotKey, ct))
                ?.DepartmentId;

            if (!depId.HasValue)
                continue;

            var leader = await FindLeaderOfDepartment(depId.Value, ct);
            if (leader == null)
                continue;

            _db.SignTasks.Add(new SignTask
            {
                DossierId = dossierId,
                AssigneeId = leader.Value,
                Order = ord++,
                SlotKey = slotKey,
                Phase = SlotPhase.Vung2,
                Status = SignTaskStatus.Pending,
                VisiblePattern = VisiblePattern(slotKey)
            });
        }

        // --- Vùng 3: 3 Phó Giám đốc ---
        foreach (var k in new[] { SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3 })
        {
            bool exists = await _db.SignTasks
                .AnyAsync(t => t.DossierId == dossierId && t.SlotKey == k, ct);

            if (exists)
                continue;

            var uid = (await _db.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slot == k, ct))
                ?.UserId;

            if (uid.HasValue)
            {
                _db.SignTasks.Add(new SignTask
                {
                    DossierId = dossierId,
                    AssigneeId = uid.Value,
                    Order = ord++,
                    SlotKey = k,
                    Phase = SlotPhase.Vung3,
                    Status = SignTaskStatus.Pending,
                    VisiblePattern = VisiblePattern(k)
                });
            }
        }

        // --- Văn thư ---
        bool clerkExists = await _db.SignTasks
            .AnyAsync(t => t.DossierId == dossierId && t.SlotKey == SlotKey.VanThuCheck, ct);

        if (!clerkExists)
        {
            var clerk = await FindAnyUserInRole("VanThu", ct);
            if (clerk != null)
            {
                _db.SignTasks.Add(new SignTask
                {
                    DossierId = dossierId,
                    AssigneeId = clerk.Value,
                    Order = ord++,
                    SlotKey = SlotKey.VanThuCheck,
                    Phase = SlotPhase.Clerk,
                    Status = SignTaskStatus.Pending,
                    VisiblePattern = VisiblePattern(SlotKey.VanThuCheck)
                });
            }
        }

        // --- Giám đốc ---
        bool gdExists = await _db.SignTasks
            .AnyAsync(t => t.DossierId == dossierId && t.SlotKey == SlotKey.GiamDoc, ct);

        if (!gdExists)
        {
            var gd = (await _db.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slot == SlotKey.GiamDoc, ct))
                ?.UserId;

            if (gd.HasValue)
            {
                _db.SignTasks.Add(new SignTask
                {
                    DossierId = dossierId,
                    AssigneeId = gd.Value,
                    Order = ord++,
                    SlotKey = SlotKey.GiamDoc,
                    Phase = SlotPhase.Director,
                    Status = SignTaskStatus.Pending,
                    VisiblePattern = VisiblePattern(SlotKey.GiamDoc)
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Recompute để bật/tắt slot sau khi đã có đầy đủ route
        await _activation.RecomputeAsync(dossier.Id, ct);
    }

    /// <summary>
    /// Đảm bảo user hiện tại có quyền render hồ sơ:
    /// - Là người tạo hồ sơ
    /// - Hoặc là Assignee của bất kỳ SignTask nào trong hồ sơ
    /// </summary>
    public async Task EnsureCanRenderAsync(Guid dossierId, Guid userId, CancellationToken ct = default)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.SignTasks)
            .FirstOrDefaultAsync(d => d.Id == dossierId, ct)
            ?? throw new InvalidOperationException("Dossier not found.");

        var isCreator = dossier.CreatedById == userId;
        var isAssignee = dossier.SignTasks.Any(t => t.AssigneeId == userId);

        if (isCreator || isAssignee)
            return;

        // Cho phép người được ủy quyền của bất kỳ Assignee nào được render hồ sơ
        var assigneeIds = dossier.SignTasks
            .Select(t => t.AssigneeId)
            .Distinct()
            .ToList();

        var now = DateTime.UtcNow;

        var isDelegate = await _db.UserDelegations.AsNoTracking()
            .AnyAsync(d =>
                d.IsActive &&
                d.ToUserId == userId &&
                assigneeIds.Contains(d.FromUserId) &&
                d.StartUtc <= now &&
                (d.EndUtc == null || d.EndUtc >= now),
                ct);

        if (isDelegate)
            return;

        // Nếu cần mở rộng, có thể cho thêm role đặc biệt (Admin, VanThu...) tại đây.
        throw new UnauthorizedAccessException("Bạn không có quyền render hồ sơ này.");
    }

    // Mapping SlotKey -> VisiblePattern trong Template
    private static string VisiblePattern(SlotKey k) => k switch
    {
        SlotKey.NguoiTrinh => "##{S1}##",
        SlotKey.LanhDaoPhong => "##{S2}##",
        SlotKey.DonViLienQuan => "##{S3}##",
        SlotKey.KHTH => "##{S4}##",
        SlotKey.HCQT => "##{S5}##",
        SlotKey.TCCB => "##{S6}##",
        SlotKey.TCKT => "##{S7}##",
        SlotKey.CTCD => "##{S8}##",
        SlotKey.PGD1 => "##{S9}##",
        SlotKey.PGD2 => "##{S10}##",
        SlotKey.PGD3 => "##{S11}##",
        SlotKey.VanThuCheck => "##{S12}##",
        SlotKey.GiamDoc => "##{S13}##",
        _ => "##{S0}##"
    };

    private async Task<Guid?> FindLeaderOfDepartment(Guid? deptId, CancellationToken ct)
    {
        if (!deptId.HasValue) return null;

        return await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u =>
                u.DepartmentId == deptId.Value &&
                u.IsActive &&
                u.UserRoles.Any(ur =>
                    ur.Role!.Name == "TruongKhoa" || ur.Role!.Name == "TruongPhong"))
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Guid?> FindAnyUserInRole(string roleName, CancellationToken ct)
        => await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u =>
                u.IsActive &&
                u.UserRoles.Any(ur => ur.Role!.Name == roleName))
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

    // Danh sách hồ sơ do user hiện tại tạo
    public async Task<IReadOnlyList<DossierListItemDto>> GetMyCreatedAsync(Guid userId, CancellationToken ct = default)
    {
        var query = _db.Dossiers
    .AsNoTracking()
    .Include(d => d.CreatedBy)
        .ThenInclude(u => u.Department)
    .Where(d => d.CreatedById == userId)
    .OrderByDescending(d => d.CreatedAt);

        var list = await query
            .Select(d => new DossierListItemDto(
                d.Id,
                d.Code,
                d.Title,
                d.Status,
                d.CreatedAt,
                d.CreatedBy != null ? d.CreatedBy.FullName : null,
                d.CreatedBy != null && d.CreatedBy.Department != null
                    ? d.CreatedBy.Department.Name
                    : null
            ))
            .ToListAsync(ct);

        return list;
    }
}
