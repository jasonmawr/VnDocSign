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
    { _db = db; _activation = activation; }

    public async Task<DossierCreateResponse> CreateAsync(DossierCreateRequest req, CancellationToken ct = default)
    {
        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.CreatedById, ct)
            ?? throw new InvalidOperationException("Creator not found.");

        var dossier = new Dossier { Title = req.Title, Code = req.Code, CreatedById = req.CreatedById, Status = DossierStatus.Draft };
        _db.Dossiers.Add(dossier);
        await _db.SaveChangesAsync(ct);

        int ord = 1;
        string P(SlotKey k) => k switch
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

        // Vùng 1
        _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = creator.Id, Order = ord++, SlotKey = SlotKey.NguoiTrinh, Phase = SlotPhase.Vung1, Status = SignTaskStatus.Pending, VisiblePattern = P(SlotKey.NguoiTrinh) });

        var leaderCreator = await FindLeaderOfDepartment(creator.DepartmentId, ct);
        if (leaderCreator != null)
            _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = leaderCreator.Value, Order = ord++, SlotKey = SlotKey.LanhDaoPhong, Phase = SlotPhase.Vung1, Status = SignTaskStatus.Pending, VisiblePattern = P(SlotKey.LanhDaoPhong) });

        if (req.RelatedDepartmentId.HasValue)
        {
            var leaderRel = await FindLeaderOfDepartment(req.RelatedDepartmentId.Value, ct);
            if (leaderRel != null)
                _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = leaderRel.Value, Order = ord++, SlotKey = SlotKey.DonViLienQuan, Phase = SlotPhase.Vung1, Status = SignTaskStatus.Pending, VisiblePattern = P(SlotKey.DonViLienQuan) });
        }

        // Vùng 2 (5 phòng chức năng)
        foreach (var k in new[] { SlotKey.KHTH, SlotKey.HCQT, SlotKey.TCCB, SlotKey.TCKT, SlotKey.CTCD })
        {
            var depId = (await _db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Slot == k, ct))?.DepartmentId;
            if (depId.HasValue)
            {
                var leader = await FindLeaderOfDepartment(depId.Value, ct);
                if (leader != null)
                    _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = leader.Value, Order = ord++, SlotKey = k, Phase = SlotPhase.Vung2, Status = SignTaskStatus.Pending, VisiblePattern = P(k) });
            }
        }

        // Vùng 3 (3 PGĐ)
        foreach (var k in new[] { SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3 })
        {
            var uid = (await _db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Slot == k, ct))?.UserId;
            if (uid.HasValue)
                _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = uid.Value, Order = ord++, SlotKey = k, Phase = SlotPhase.Vung3, Status = SignTaskStatus.Pending, VisiblePattern = P(k) });
        }

        // Văn thư & Giám đốc
        var clerk = await FindAnyUserInRole("VanThu", ct);
        if (clerk != null)
            _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = clerk.Value, Order = ord++, SlotKey = SlotKey.VanThuCheck, Phase = SlotPhase.Clerk, Status = SignTaskStatus.Pending, VisiblePattern = P(SlotKey.VanThuCheck) });

        var gd = (await _db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Slot == SlotKey.GiamDoc, ct))?.UserId;
        if (gd.HasValue)
            _db.SignTasks.Add(new SignTask { DossierId = dossier.Id, AssigneeId = gd.Value, Order = ord++, SlotKey = SlotKey.GiamDoc, Phase = SlotPhase.Director, Status = SignTaskStatus.Pending, VisiblePattern = P(SlotKey.GiamDoc) });

        await _db.SaveChangesAsync(ct);

        dossier.Status = DossierStatus.Submitted;
        await _activation.RecomputeAsync(dossier.Id, ct);

        return new DossierCreateResponse(dossier.Id);
    }

    private async Task<Guid?> FindLeaderOfDepartment(Guid deptId, CancellationToken ct)
        => await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.DepartmentId == deptId && u.IsActive && u.UserRoles.Any(ur => ur.Role!.Name == "TruongKhoa" || ur.Role!.Name == "TruongPhong"))
            .Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);

    private async Task<Guid?> FindAnyUserInRole(string roleName, CancellationToken ct)
        => await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role!.Name == roleName))
            .Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
}
