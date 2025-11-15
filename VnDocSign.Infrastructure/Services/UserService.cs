using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Users;
using VnDocSign.Application.Contracts.Interfaces.Users;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly AppDbContext _db;
    public UserService(AppDbContext db) => _db = db;

    public async Task<UserCreateResponse> CreateAsync(UserCreateRequest req, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(x => x.Username == req.Username, ct))
            throw new InvalidOperationException("Username exists.");

        var u = new User
        {
            Username = req.Username,
            // ===== FIXED: HASH PASSWORD =====
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.FullName,
            Email = req.Email,
            DepartmentId = req.DepartmentId,
            IsActive = true
        };

        _db.Users.Add(u);
        await _db.SaveChangesAsync(ct);
        return new UserCreateResponse(u.Id);
    }

    public async Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.AsNoTracking()
            .Select(u => new UserListItem(u.Id, u.Username, u.FullName, u.Email, u.IsActive, u.DepartmentId))
            .ToListAsync(ct);

    // ================== ROLES ==================

    public async Task<UserWithRolesDto> GetWithRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct)
                ?? throw new KeyNotFoundException("User not found");

        var roles = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           where ur.UserId == userId
                           select r.Name).ToListAsync(ct);

        return new UserWithRolesDto(u.Id, u.Username, u.FullName, u.Email, u.IsActive, u.DepartmentId, roles);
    }

    public async Task<UserWithRolesDto> AssignRolesAsync(Guid userId, AssignRolesRequest req, CancellationToken ct = default)
    {
        if (req.Roles is not { Count: > 0 }) return await GetWithRolesAsync(userId, ct);

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
                ?? throw new KeyNotFoundException("User not found");

        var names = req.Roles
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0) return await GetWithRolesAsync(userId, ct);

        // Lấy (hoặc auto-create ở DEV) các Role theo tên
        var roles = await _db.Roles.Where(r => names.Contains(r.Name)).ToListAsync(ct);
        var missing = names.Except(roles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var n in missing)
        {
            var role = new Role { Name = n };
            roles.Add(role);
            _db.Roles.Add(role);
        }

        // Tránh trùng liên kết
        var existingRoleIds = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync(ct);

        foreach (var r in roles)
        {
            if (!existingRoleIds.Contains(r.Id))
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = r.Id,
                    Username = u.Username,
                    FullName = u.FullName
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetWithRolesAsync(userId, ct);
    }

    public async Task<UserWithRolesDto> RemoveRolesAsync(Guid userId, AssignRolesRequest req, CancellationToken ct = default)
    {
        if (req.Roles is not { Count: > 0 }) return await GetWithRolesAsync(userId, ct);

        var roleIds = await _db.Roles
            .Where(r => req.Roles.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roleIds.Count == 0) return await GetWithRolesAsync(userId, ct);

        var links = await _db.UserRoles
            .Where(ur => ur.UserId == userId && roleIds.Contains(ur.RoleId))
            .ToListAsync(ct);

        if (links.Count > 0)
        {
            _db.UserRoles.RemoveRange(links);
            await _db.SaveChangesAsync(ct);
        }

        return await GetWithRolesAsync(userId, ct);
    }
}
