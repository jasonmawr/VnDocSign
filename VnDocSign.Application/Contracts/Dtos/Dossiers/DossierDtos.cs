using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Dossiers;

namespace VnDocSign.Application.Contracts.Dtos.Dossiers;

public sealed record DossierCreateRequest(string Title, string Code, Guid CreatedById, Guid? RelatedDepartmentId);
public sealed record DossierCreateResponse(Guid Id);

// === DTO cho danh sách hồ sơ ===
public sealed record DossierListItemDto(
    Guid Id,
    string Code,
    string Title,
    DossierStatus Status,
    DateTime CreatedAt,
    string? CreatedByFullName,
    string? DepartmentName
);
