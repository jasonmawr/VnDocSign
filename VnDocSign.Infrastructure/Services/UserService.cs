using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Users;
using VnDocSign.Application.Contracts.Interfaces.Users;
using VnDocSign.Domain.Entities;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly AppDbContext _db;
    public UserService(AppDbContext db) => _db = db;

    public async Task<UserCreateResponse> CreateAsync(UserCreateRequest req, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(x => x.Username == req.Username, ct)) throw new InvalidOperationException("Username exists.");
        var u = new User { Username = req.Username, PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password), FullName = req.FullName, Email = req.Email, DepartmentId = req.DepartmentId, IsActive = true };
        _db.Users.Add(u); await _db.SaveChangesAsync(ct);
        return new UserCreateResponse(u.Id);
    }

    public async Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.AsNoTracking().Select(u => new UserListItem(u.Id, u.Username, u.FullName, u.Email, u.IsActive, u.DepartmentId)).ToListAsync(ct);
}
