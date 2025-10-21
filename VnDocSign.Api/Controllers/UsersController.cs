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

    [HttpGet] public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _svc.GetAllAsync(ct));
    [HttpPost] public async Task<IActionResult> Create([FromBody] UserCreateRequest req, CancellationToken ct) => Ok(await _svc.CreateAsync(req, ct));
}
