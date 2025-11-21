namespace VnDocSign.Application.Contracts.Dtos.Users
{
    public sealed record UserDelegationDto(
        Guid Id,
        Guid FromUserId,
        Guid ToUserId,
        string? FromFullName,
        string? ToFullName,
        DateTime StartUtc,
        DateTime? EndUtc,
        bool IsActive
    );

    public sealed record UserDelegationCreateRequest(
        Guid ToUserId,
        DateTime StartUtc,
        DateTime? EndUtc
    );

    public sealed record UserDelegationUpdateRequest(
        DateTime StartUtc,
        DateTime? EndUtc,
        bool IsActive
    );
}
