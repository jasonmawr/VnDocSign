using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Auth;
using VnDocSign.Application.Contracts.Interfaces.Auth;
using VnDocSign.Application.Contracts.Interfaces.Security;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var username = req.Username.Trim().ToLower();

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                u => u.Username.ToLower() == username,
                ct
            );

        if (user is null)
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản đang bị vô hiệu hóa.");

        var valid = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
        if (!valid)
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");

        var roles = user.UserRoles
            .Select(r => r.Role!.Name)
            .ToArray();

        var token = _jwt.CreateToken(user.Id, user.Username, roles);

        return new LoginResponse(
            Token: token,
            UserId: user.Id,
            Username: user.Username,
            FullName: user.FullName,
            Roles: roles
        );
    }
}
