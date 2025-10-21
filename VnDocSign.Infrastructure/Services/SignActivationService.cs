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
        var tasks = await _db.SignTasks.Where(t => t.DossierId == dossierId).ToListAsync(ct);
        foreach (var t in tasks) t.IsActivated = false;

        bool Done(SlotKey k) => tasks.Any(x => x.SlotKey == k && x.Status == SignTaskStatus.Approved);
        bool Exists(SlotKey k) => tasks.Any(x => x.SlotKey == k);

        ActivateFirst(tasks, SlotKey.NguoiTrinh);
        if (Done(SlotKey.NguoiTrinh)) ActivateFirst(tasks, SlotKey.LanhDaoPhong);
        if (Done(SlotKey.LanhDaoPhong) && Exists(SlotKey.DonViLienQuan)) ActivateFirst(tasks, SlotKey.DonViLienQuan);

        var v1Done = Done(SlotKey.NguoiTrinh) && Done(SlotKey.LanhDaoPhong) &&
                     (!Exists(SlotKey.DonViLienQuan) || Done(SlotKey.DonViLienQuan));

        if (v1Done)
            foreach (var k in new[] { SlotKey.KHTH, SlotKey.HCQT, SlotKey.TCCB, SlotKey.TCKT, SlotKey.CTCD })
                ActivateAll(tasks, k);

        var v2Done = new[] { SlotKey.KHTH, SlotKey.HCQT, SlotKey.TCCB, SlotKey.TCKT, SlotKey.CTCD }
                        .All(k => !Exists(k) || Done(k));

        if (v1Done && v2Done)
            foreach (var k in new[] { SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3 })
                ActivateAll(tasks, k);

        var pgdDone = new[] { SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3 }
                        .All(k => !Exists(k) || Done(k));
        if (v1Done && v2Done && pgdDone)
        {
            ActivateAll(tasks, SlotKey.VanThuCheck);
            if (tasks.Any(t => t.SlotKey == SlotKey.VanThuCheck && t.ClerkConfirmed))
                ActivateFirst(tasks, SlotKey.GiamDoc);
        }

        await _db.SaveChangesAsync(ct);

        static void ActivateFirst(List<SignTask> t, SlotKey k)
        { var x = t.FirstOrDefault(a => a.SlotKey == k && a.Status == SignTaskStatus.Pending); if (x != null) x.IsActivated = true; }

        static void ActivateAll(List<SignTask> t, SlotKey k)
        { foreach (var x in t.Where(a => a.SlotKey == k && a.Status == SignTaskStatus.Pending)) x.IsActivated = true; }
    }
}
