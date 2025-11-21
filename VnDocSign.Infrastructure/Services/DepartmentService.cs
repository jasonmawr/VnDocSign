using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Departments;
using VnDocSign.Application.Contracts.Interfaces.Departments;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;

    public DepartmentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<DepartmentResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Departments
            .AsNoTracking()
            .OrderBy(d => d.Code)
            .Select(d => new DepartmentResponse(
                d.Id,
                d.Code,
                d.Name,
                d.IsActive))
            .ToListAsync(ct);
    }

    public async Task<DepartmentResponse> CreateAsync(DepartmentCreateRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            throw new ArgumentException("Department code is required.", nameof(req.Code));

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ArgumentException("Department name is required.", nameof(req.Name));

        var code = req.Code.Trim();
        var name = req.Name.Trim();

        // So sánh mã phòng ban không phân biệt hoa thường
        var exists = await _db.Departments
            .AnyAsync(d => d.Code.ToLower() == code.ToLower(), ct);

        if (exists)
            throw new InvalidOperationException($"Department code '{code}' already exists.");

        var dep = new Department
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            IsActive = req.IsActive
        };

        _db.Departments.Add(dep);
        await _db.SaveChangesAsync(ct);

        return new DepartmentResponse(
            dep.Id,
            dep.Code,
            dep.Name,
            dep.IsActive);
    }

    public async Task<DepartmentResponse> UpdateAsync(Guid id, DepartmentCreateRequest req, CancellationToken ct = default)
    {
        var dep = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (dep is null)
            throw new KeyNotFoundException("Department not found.");

        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ArgumentException("Department name is required.", nameof(req.Name));

        // Code được xem là immutable sau khi tạo, nên bỏ qua req.Code.
        dep.Name = req.Name.Trim();

        await _db.SaveChangesAsync(ct);

        return new DepartmentResponse(
            dep.Id,
            dep.Code,
            dep.Name,
            dep.IsActive);
    }

    public async Task<DepartmentResponse> ToggleActiveAsync(Guid id, CancellationToken ct = default)
    {
        var dep = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (dep is null)
            throw new KeyNotFoundException("Department not found.");

        var newIsActive = !dep.IsActive;

        // Khi vô hiệu hóa: không cho phép nếu còn user active trong phòng đó
        if (!newIsActive)
        {
            var hasActiveUsers = await _db.Users
                .AnyAsync(u => u.DepartmentId == dep.Id && u.IsActive, ct);

            if (hasActiveUsers)
            {
                throw new InvalidOperationException(
                    "Không thể vô hiệu hóa phòng ban vì vẫn còn người dùng đang hoạt động thuộc phòng này. " +
                    "Vui lòng chuyển hoặc vô hiệu hóa người dùng trước.");
            }
        }

        dep.IsActive = newIsActive;

        await _db.SaveChangesAsync(ct);

        return new DepartmentResponse(
            dep.Id,
            dep.Code,
            dep.Name,
            dep.IsActive);
    }
}
