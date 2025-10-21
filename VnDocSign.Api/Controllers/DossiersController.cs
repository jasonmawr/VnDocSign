namespace VnDocSign.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Contracts.Dtos.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;

[Authorize]
[ApiController]
[Route("api/dossiers")]
public sealed class DossiersController : ControllerBase
{
    private readonly IDossierService _svc;
    public DossiersController(IDossierService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DossierCreateRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));
}
