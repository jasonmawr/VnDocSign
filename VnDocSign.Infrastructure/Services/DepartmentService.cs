
using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Departments;
using VnDocSign.Application.Contracts.Interfaces.Departments;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services;

public sealed class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;
    public DepartmentService(AppDbContext db) => _db = db;

    public async Task<DepartmentResponse> CreateAsync(DepartmentCreateRequest req, CancellationToken ct = default)
    {
        if (await _db.Departments.AnyAsync(d => d.Code == req.Code, ct))
            throw new InvalidOperationException("Department code already exists.");

        var d = new Department { Code = req.Code, Name = req.Name, IsActive = req.IsActive };
        _db.Departments.Add(d);
        await _db.SaveChangesAsync(ct);
        return new DepartmentResponse(d.Id, d.Code, d.Name, d.IsActive);
    }

    public async Task<IReadOnlyList<DepartmentResponse>> GetAllAsync(CancellationToken ct = default)
        => await _db.Departments.AsNoTracking()
            .Select(d => new DepartmentResponse(d.Id, d.Code, d.Name, d.IsActive))
            .ToListAsync(ct);
}
