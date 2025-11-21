namespace VnDocSign.Application.Contracts.Dtos.Auth;

public sealed record LoginRequest(
    string Username,
    string Password
);

public sealed record LoginResponse(
    string Token,
    Guid UserId,
    string Username,
    string FullName,
    string[] Roles
);
