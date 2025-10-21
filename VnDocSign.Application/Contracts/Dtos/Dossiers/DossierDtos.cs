using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Dossiers;

public sealed record DossierCreateRequest(string Title, string Code, Guid CreatedById, Guid? RelatedDepartmentId);
public sealed record DossierCreateResponse(Guid Id);

