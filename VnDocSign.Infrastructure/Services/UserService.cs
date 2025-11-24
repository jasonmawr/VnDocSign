using DocumentFormat.OpenXml.Spreadsheet;
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
        // Normalize input: list of GUID strings
        var normalized = req.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return await GetWithRolesAsync(userId, ct);

        // Load user để lấy Username, FullName
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
                   ?? throw new KeyNotFoundException("User not found.");

        // Validate GUID format
        var invalidGuids = normalized
            .Where(x => !Guid.TryParse(x, out _))
            .ToList();

        if (invalidGuids.Any())
            throw new InvalidOperationException($"Invalid RoleId format: {string.Join(", ", invalidGuids)}");

        // Convert to Guid
        var roleGuids = normalized
            .Select(Guid.Parse)
            .ToList();

        // Load roles by ID
        var existingRoles = await _db.Roles
            .Where(r => roleGuids.Contains(r.Id))
            .ToListAsync(ct);

        // Check missing RoleIds
        var missingIds = roleGuids.Except(existingRoles.Select(r => r.Id)).ToList();
        if (missingIds.Any())
            throw new InvalidOperationException(
                $"Role(s) not found: {string.Join(", ", missingIds)}"
            );

        // Clear old roles
        _db.UserRoles.RemoveRange(
            _db.UserRoles.Where(ur => ur.UserId == userId)
        );

        // Assign new ones
        foreach (var role in existingRoles)
        {
            _db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = role.Id,
                Username = user.Username,
                FullName = user.FullName,
                RoleName = role.Name   // lưu để quản lý nhìn cho dễ
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetWithRolesAsync(userId, ct);
    }

    // REMOVE ROLES
    public async Task<UserWithRolesDto> RemoveRolesAsync(Guid userId, AssignRolesRequest req, CancellationToken ct = default)
    {
        // Normalize input: GUID strings
        var normalized = req.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
            return await GetWithRolesAsync(userId, ct);

        // Validate GUID format
        var invalidGuids = normalized
            .Where(x => !Guid.TryParse(x, out _))
            .ToList();

        if (invalidGuids.Any())
            throw new InvalidOperationException($"Invalid RoleId format: {string.Join(", ", invalidGuids)}");

        var roleGuids = normalized
            .Select(Guid.Parse)
            .ToList();

        // Lấy các link user-role cần xoá theo RoleId
        var links = await _db.UserRoles
            .Where(ur => ur.UserId == userId && roleGuids.Contains(ur.RoleId))
            .ToListAsync(ct);

        if (links.Any())
        {
            _db.UserRoles.RemoveRange(links);
            await _db.SaveChangesAsync(ct);
        }

        return await GetWithRolesAsync(userId, ct);
    }

    //EMPLOYEECODE
    public async Task<UserWithRolesDto> UpdateEmployeeCodeAsync(
    Guid userId,
    UpdateEmployeeCodeRequest req,
    CancellationToken ct = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        // Chuẩn hóa: trim, cho phép null để xoá mã nếu cần
        user.EmployeeCode = string.IsNullOrWhiteSpace(req.EmployeeCode)
            ? null
            : req.EmployeeCode.Trim();

        await _db.SaveChangesAsync(ct);

        // Trả về always dùng DTO chuẩn có roles
        return await GetWithRolesAsync(userId, ct);
    }
}
