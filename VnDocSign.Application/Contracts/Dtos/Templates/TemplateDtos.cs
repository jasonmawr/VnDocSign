using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Dtos.Templates;

public sealed record TemplateListItemDto(
    int Id, string Code, string Name, bool IsActive,
    int? LatestVersionId, int? LatestVersionNo, string? LatestDocx, string? LatestPdf, DateTime? LatestCreatedAt);

public sealed record TemplateDetailDto(
    int Id, string Code, string Name, bool IsActive,
    IReadOnlyList<TemplateVersionDto> Versions);

public sealed record TemplateVersionDto(
    int Id, int VersionNo, string FileNameDocx, string FileNamePdf,
    IReadOnlyList<string> VisiblePatterns, string? Notes, DateTime CreatedAt);

public sealed record TemplateUploadResultDto(
    int TemplateId, int VersionId, int VersionNo,
    string FileNameDocx, string FileNamePdf, IReadOnlyList<string> VisiblePatterns);

public sealed record TemplateUploadRequest(
    string TemplateCode, string? Name, string? Notes);
