using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VnDocSign.Application.Contracts.Dtos.Templates;

namespace VnDocSign.Application.Contracts.Interfaces.Documents;

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<TemplateDetailDto> GetDetailAsync(int id, CancellationToken ct = default);

    Task<TemplateUploadResultDto> UploadAsync(
        TemplateUploadRequest meta,
        IFormFile file, Guid? userId,
        CancellationToken ct = default);

    Task ToggleActiveAsync(int id, CancellationToken ct = default);
    Task DeleteVersionAsync(int versionId, CancellationToken ct = default);
}