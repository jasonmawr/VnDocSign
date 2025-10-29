using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Dossiers;

public sealed record DossierContentUpsertRequest(
    string? TemplateCode,
    IDictionary<string, string?> Fields // các key thân thiện : donVi, vuViec....
    );

public sealed record DossierContentDto(
    string? TemplateCode,
    IReadOnlyDictionary<string, string?> FieldsByAlias // trả về các alias chuẩn : DON_VI, VU_VIEC....
    );