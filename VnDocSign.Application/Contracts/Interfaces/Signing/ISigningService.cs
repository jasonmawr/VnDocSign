using VnDocSign.Application.Contracts.Dtos.Signing;

namespace VnDocSign.Application.Contracts.Interfaces.Signing;

public interface ISigningService
{
    Task ApproveAndSignAsync(ApproveRequest req, CancellationToken ct = default); // PHASE 1: chỉ approve, chưa ký số
    Task RejectAsync(RejectRequest req, CancellationToken ct = default);
    Task ClerkConfirmAsync(ClerkConfirmRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<MyTaskItem>> GetMyTasksAsync(Guid userId, CancellationToken ct = default);
}
