using VnDocSign.Application.Contracts.Dtos.Signing;

namespace VnDocSign.Application.Contracts.Interfaces.Signing;

public interface ISigningService
{
    /// <summary>
    /// Phê duyệt task hiện tại và thực hiện ký:
    /// - Nếu Ssm:Mode = "Mock" hoặc Pin rỗng => ký MOCK (PNG, RenderSignedMockAsync).
    /// - Ngược lại => ký số thật qua SSM (SignPdfAsync).
    /// </summary>
    Task ApproveAndSignAsync(ApproveRequest req, CancellationToken ct = default);

    Task RejectAsync(RejectRequest req, CancellationToken ct = default);

    Task ClerkConfirmAsync(ClerkConfirmRequest req, CancellationToken ct = default);

    // <summary>
    /// Danh sách task đang chờ tôi ký (đã có sẵn từ trước).
    /// </summary>
    Task<IReadOnlyList<MyTaskItem>> GetMyTasksAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// GĐ5: Danh sách task của tôi (grouped: pending / processed / completed).
    /// </summary>
    Task<MyTasksGroupedDto> GetMyTasksGroupedAsync(Guid userId, CancellationToken ct = default);
}
