using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Signing;
using VnDocSign.Application.Contracts.Interfaces.Signing;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Infrastructure.Persistence;


namespace VnDocSign.Infrastructure.Services;
public sealed class SigningService : ISigningService
{
    private readonly AppDbContext _db;
    private readonly ISignActivationService _activation;

    public SigningService(AppDbContext db, ISignActivationService activation)
    { _db = db; _activation = activation; }

    public async Task ApproveAndSignAsync(ApproveRequest req, CancellationToken ct = default)
    {
        var t = await _db.SignTasks.Include(x => x.Dossier).FirstOrDefaultAsync(x => x.Id == req.TaskId, ct)
            ?? throw new InvalidOperationException("Task not found.");
        if (!t.IsActivated || t.Status != SignTaskStatus.Pending) throw new InvalidOperationException("Not ready.");
        if (t.AssigneeId != req.ActorUserId) throw new UnauthorizedAccessException();

        // PHASE 1: chỉ approve (chưa ký số)
        t.Status = SignTaskStatus.Approved;
        t.DecidedAt = DateTime.UtcNow;
        t.Comment = req.Comment;

        var anyPending = await _db.SignTasks.AnyAsync(x => x.DossierId == t.DossierId && x.Status == SignTaskStatus.Pending, ct);
        t.Dossier!.Status = anyPending ? DossierStatus.InProgress : DossierStatus.Approved;

        await _db.SaveChangesAsync(ct);
        await _activation.RecomputeAsync(t.DossierId, ct);
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

        // *Lưu ý*: ở PHASE 1 mình chưa check vt.AssigneeId == req.ActorUserId.
        // Nếu bạn muốn chặt chẽ, mở khoá dòng dưới:
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
