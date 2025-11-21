using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Departments;

namespace VnDocSign.Application.Contracts.Interfaces.Departments;

public interface IDepartmentService
{
    Task<IReadOnlyCollection<DepartmentResponse>> GetAllAsync(CancellationToken ct = default);

    Task<DepartmentResponse> CreateAsync(DepartmentCreateRequest req, CancellationToken ct = default);

    /// <summary>
    /// Cập nhật thông tin phòng ban.
    /// - Chỉ cho phép cập nhật Name, không cho phép đổi Code.
    /// </summary>
    Task<DepartmentResponse> UpdateAsync(Guid id, DepartmentCreateRequest req, CancellationToken ct = default);

    /// <summary>
    /// Bật/tắt trạng thái hoạt động của phòng ban.
    /// - Khi chuyển từ Active -> Inactive: không cho phép nếu còn user active trong phòng đó.
    /// </summary>
    Task<DepartmentResponse> ToggleActiveAsync(Guid id, CancellationToken ct = default);
}
