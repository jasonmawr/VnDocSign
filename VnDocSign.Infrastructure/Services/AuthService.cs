using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Auth;
using VnDocSign.Application.Contracts.Interfaces.Auth;
using VnDocSign.Application.Contracts.Interfaces.Security; // <-- thêm dòng này
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;  // <-- kiểu ở Application

    public AuthService(AppDbContext db, IJwtTokenService jwt)
    { _db = db; _jwt = jwt; }

    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var roles = user.UserRoles.Select(r => r.Role!.Name).ToArray();
        var token = _jwt.CreateToken(user.Id, user.Username, roles);

        return new LoginResponse(token, user.Id, user.Username, user.FullName, roles);
    }
}
