namespace VnDocSign.Application.Contracts.Interfaces.Security;

/// <summary>
/// Service tạo JWT token cho người dùng.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Tạo JWT token với UserId, Username và Roles.
    /// </summary>
    string CreateToken(Guid userId, string username, IEnumerable<string> roles);
}
