using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Dossiers;

namespace VnDocSign.Application.Contracts.Interfaces.Dossiers;

public interface IDossierService
{
    Task<DossierCreateResponse> CreateAsync(DossierCreateRequest req, CancellationToken ct = default);
}

