using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Departments;

public sealed record DepartmentCreateRequest(string Code, string Name, bool IsActive);
public sealed record DepartmentResponse(Guid Id, string Code, string Name, bool IsActive);
