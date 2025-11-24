using System;
using System.Threading;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Dossiers;

namespace VnDocSign.Application.Contracts.Interfaces.Dossiers;

public interface IDossierContentService
{
    Task<DossierContentDto> GetAsync(Guid dossierId, CancellationToken ct = default);

    Task<DossierContentDto> UpsertAsync(
        Guid dossierId,
        DossierContentUpsertRequest req,
        Guid? userId,
        CancellationToken ct = default);
}
