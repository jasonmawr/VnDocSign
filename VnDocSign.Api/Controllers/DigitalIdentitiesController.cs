using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Contracts.Dtos.DigitalIdentities;
using VnDocSign.Application.Contracts.Interfaces.DigitalIdentities;

[ApiController]
[Route("api/digital-identities")]
[Authorize(Policy = "RequireAdmin")]
public sealed class DigitalIdentitiesController : ControllerBase
{
    private readonly IDigitalIdentityService _svc;
    public DigitalIdentitiesController(IDigitalIdentityService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DigitalIdentityDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _svc.GetAsync(id, ct));

    [HttpGet("by-user/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DigitalIdentityDto>>> ListByUser(Guid userId, CancellationToken ct)
        => Ok(await _svc.ListByUserAsync(userId, ct));

    [HttpPost]
    public async Task<ActionResult<DigitalIdentityDto>> Create([FromBody] DigitalIdentityCreateRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DigitalIdentityDto>> Update(Guid id, [FromBody] DigitalIdentityUpdateRequest req, CancellationToken ct)
        => Ok(await _svc.UpdateAsync(id, req, ct)); // <-- dòng đã sửa

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }
}
