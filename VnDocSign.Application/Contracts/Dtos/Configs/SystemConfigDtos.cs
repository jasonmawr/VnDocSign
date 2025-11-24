using System;
using System.Collections.Generic;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Application.Contracts.Dtos.Configs;

public sealed record SystemConfigItemDto(
    int Id,
    SlotKey Slot,
    string SlotDisplayName,
    bool IsFunctionalDepartment, // true = slot Vùng 2: map theo Department
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    Guid? UserId,
    string? UserFullName,
    bool IsActive
);

public sealed record SystemConfigUpdateRequest(
    Guid? DepartmentId,
    Guid? UserId,
    bool? IsActive
);
