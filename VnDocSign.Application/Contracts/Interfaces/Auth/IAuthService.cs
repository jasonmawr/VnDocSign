using VnDocSign.Application.Contracts.Dtos.Auth;

namespace VnDocSign.Application.Contracts.Interfaces.Auth;

/// <summary>
/// Service xử lý xác thực người dùng.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Đăng nhập và tạo JWT Token.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
}
