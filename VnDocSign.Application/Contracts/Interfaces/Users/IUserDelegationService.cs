using VnDocSign.Application.Contracts.Dtos.Users;

namespace VnDocSign.Application.Contracts.Interfaces.Users
{
    public interface IUserDelegationService
    {
        /// <summary>Danh sách ủy quyền mà user hiện tại là người ủy quyền (FromUser).</summary>
        Task<IReadOnlyList<UserDelegationDto>> GetOwnedAsync(Guid ownerUserId, CancellationToken ct = default);

        /// <summary>Danh sách ủy quyền mà user hiện tại là người được ủy (ToUser).</summary>
        Task<IReadOnlyList<UserDelegationDto>> GetDelegatedToMeAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Tạo ủy quyền mới (FromUser = ownerUserId).</summary>
        Task<UserDelegationDto> CreateAsync(
            Guid ownerUserId,
            UserDelegationCreateRequest req,
            CancellationToken ct = default);

        /// <summary>Cập nhật ủy quyền (chỉ FromUser được sửa).</summary>
        Task<UserDelegationDto> UpdateAsync(
            Guid ownerUserId,
            Guid delegationId,
            UserDelegationUpdateRequest req,
            CancellationToken ct = default);

        /// <summary>Hủy ủy quyền (chỉ FromUser được hủy).</summary>
        Task DeleteAsync(Guid ownerUserId, Guid delegationId, CancellationToken ct = default);
    }
}
