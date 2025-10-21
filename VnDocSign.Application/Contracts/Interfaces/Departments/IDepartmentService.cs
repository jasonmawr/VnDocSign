using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Departments;

namespace VnDocSign.Application.Contracts.Interfaces.Departments
{
    public interface IDepartmentService
    {
        Task<DepartmentResponse> CreateAsync(DepartmentCreateRequest req, CancellationToken ct = default);
        Task<IReadOnlyList<DepartmentResponse>> GetAllAsync(CancellationToken ct = default);
    }
}
