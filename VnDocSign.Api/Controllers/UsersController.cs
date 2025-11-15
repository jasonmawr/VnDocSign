using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Contracts.Dtos.Users;
using VnDocSign.Application.Contracts.Interfaces.Users;

namespace VnDocSign.Api.Controllers;

[Authorize(Policy = "RequireAdmin")]
[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _svc;
    public UsersController(IUserService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _svc.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest req, CancellationToken ct)
        => Ok(await _svc.CreateAsync(req, ct));

    // ===== NEW: xem roles của 1 user =====
    [HttpGet("{userId:guid}/roles")]
    public async Task<ActionResult<UserWithRolesDto>> GetRoles([FromRoute] Guid userId, CancellationToken ct)
        => Ok(await _svc.GetWithRolesAsync(userId, ct));

    // ===== NEW: gán roles cho 1 user =====
    [HttpPost("{userId:guid}/roles")]
    public async Task<ActionResult<UserWithRolesDto>> AssignRoles([FromRoute] Guid userId, [FromBody] AssignRolesRequest req, CancellationToken ct)
        => Ok(await _svc.AssignRolesAsync(userId, req, ct));

    // ===== NEW: bỏ 1 số roles khỏi user =====
    [HttpDelete("{userId:guid}/roles")]
    public async Task<ActionResult<UserWithRolesDto>> RemoveRoles([FromRoute] Guid userId, [FromBody] AssignRolesRequest req, CancellationToken ct)
        => Ok(await _svc.RemoveRolesAsync(userId, req, ct));
}
