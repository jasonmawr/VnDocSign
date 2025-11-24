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

    // CREATE USER
    public async Task<UserCreateResponse> CreateAsync(UserCreateRequest req, CancellationToken ct = default)
    {
        var username = req.Username.Trim().ToLower();

        if (await _db.Users.AnyAsync(x => x.Username.ToLower() == username, ct))
            throw new InvalidOperationException("Username already exists.");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim(),
            DepartmentId = req.DepartmentId,
            IsActive = true,
            // EmployeeCode hiện chưa có – sẽ đồng bộ từ HRM sau
            EmployeeCode = null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new UserCreateResponse(user.Id);
    }

    // GET ALL USERS
    public async Task<IReadOnlyList<UserListItem>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Select(u => new UserListItem(
                u.Id,
                u.Username,
                u.FullName,
                u.Email,
                u.IsActive,
                u.DepartmentId,
                u.EmployeeCode
            ))
            .ToListAsync(ct);
    }

    // GET USER + ROLES
    public async Task<UserWithRolesDto> GetWithRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var roles = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.Role!.Name)
            .ToListAsync(ct);

        return new UserWithRolesDto(
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            user.IsActive,
            user.DepartmentId,
            user.EmployeeCode,
            roles
        );
    }

    // ASSIGN ROLES
    public async Task<UserWithRolesDto> AssignRolesAsync(Guid userId, AssignRolesRequest req, CancellationToken ct = default)
    {
        var normalized = req.Roles
            .Select(r => r.Trim())
            .Where(r => r != "")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return await GetWithRolesAsync(userId, ct);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
                   ?? throw new KeyNotFoundException("User not found.");

        var existingRoles = await _db.Roles
            .Where(r => normalized.Contains(r.Name))
            .ToListAsync(ct);

        var missing = normalized.Except(existingRoles.Select(r => r.Name), StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var roleName in missing)
            throw new InvalidOperationException($"Role '{roleName}' does not exist.");

        var existingRoleIds = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync(ct);

        foreach (var role in existingRoles)
        {
            if (!existingRoleIds.Contains(role.Id))
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = role.Id,
                    Username = user.Username,
                    FullName = user.FullName
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetWithRolesAsync(userId, ct);
    }

    // REMOVE ROLES
    public async Task<UserWithRolesDto> RemoveRolesAsync(Guid userId, AssignRolesRequest req, CancellationToken ct = default)
    {
        var normalized = req.Roles
            .Select(r => r.Trim())
            .Where(r => r != "")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return await GetWithRolesAsync(userId, ct);

        var links = await _db.UserRoles
            .Where(ur => ur.UserId == userId && normalized.Contains(ur.Role!.Name))
            .ToListAsync(ct);

        if (links.Any())
        {
            _db.UserRoles.RemoveRange(links);
            await _db.SaveChangesAsync(ct);
        }

        return await GetWithRolesAsync(userId, ct);
    }
}
