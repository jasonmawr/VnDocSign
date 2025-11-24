using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Auth;
using VnDocSign.Application.Contracts.Interfaces.Auth;

namespace VnDocSign.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _svc;
    public AuthController(IAuthService svc) => _svc = svc;

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var data = await _svc.LoginAsync(req, ct);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(data, "Đăng nhập thành công."));
    }
}
