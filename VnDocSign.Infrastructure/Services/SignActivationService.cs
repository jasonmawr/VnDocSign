using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class SignActivationService : ISignActivationService
{
    private readonly AppDbContext _db;
    public SignActivationService(AppDbContext db) => _db = db;

    public async Task RecomputeAsync(Guid dossierId, CancellationToken ct = default)
    {
        var tasks = await _db.SignTasks
            .Where(t => t.DossierId == dossierId)
            .ToListAsync(ct);

        // Reset trạng thái activate
        foreach (var t in tasks)
            t.IsActivated = false;

        // Helpers
        bool Done(SlotKey k) => tasks.Any(x => x.SlotKey == k && x.Status == SignTaskStatus.Approved);
        bool Exists(SlotKey k) => tasks.Any(x => x.SlotKey == k);

        void ActivateFirst(SlotKey k)
        {
            var x = tasks.FirstOrDefault(a => a.SlotKey == k && a.Status == SignTaskStatus.Pending);
            if (x != null) x.IsActivated = true;
        }

        void ActivateAll(params SlotKey[] keys)
        {
            foreach (var k in keys)
                foreach (var t in tasks.Where(a => a.SlotKey == k && a.Status == SignTaskStatus.Pending))
                    t.IsActivated = true;
        }

        // =======================================================
        // VÙNG 1
        // =======================================================
        ActivateFirst(SlotKey.NguoiTrinh);

        if (Done(SlotKey.NguoiTrinh))
            ActivateFirst(SlotKey.LanhDaoPhong);

        if (Done(SlotKey.LanhDaoPhong) && Exists(SlotKey.DonViLienQuan))
            ActivateFirst(SlotKey.DonViLienQuan);

        bool v1Done =
            Done(SlotKey.NguoiTrinh) &&
            Done(SlotKey.LanhDaoPhong) &&
            (!Exists(SlotKey.DonViLienQuan) || Done(SlotKey.DonViLienQuan));

        // =======================================================
        // VÙNG 2 (5 phòng chức năng)
        // =======================================================
        SlotKey[] v2 = { SlotKey.KHTH, SlotKey.HCQT, SlotKey.TCCB, SlotKey.TCKT, SlotKey.CTCD };

        if (v1Done)
            ActivateAll(v2);

        bool v2Done = v2.All(k => !Exists(k) || Done(k));

        // =======================================================
        // VÙNG 3 (3 PGĐ)
        // =======================================================
        SlotKey[] pgd = { SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3 };

        if (v1Done && v2Done)
            ActivateAll(pgd);

        bool pgdDone = pgd.All(k => !Exists(k) || Done(k));

        // =======================================================
        // VĂN THƯ
        // =======================================================
        if (v1Done && v2Done && pgdDone)
            ActivateFirst(SlotKey.VanThuCheck);

        // =======================================================
        // GIÁM ĐỐC (chỉ mở sau khi Văn thư Confirm)
        // =======================================================
        var vt = tasks.FirstOrDefault(t => t.SlotKey == SlotKey.VanThuCheck);
        if (vt != null && vt.ClerkConfirmed)
            ActivateFirst(SlotKey.GiamDoc);

        await _db.SaveChangesAsync(ct);
    }
}
