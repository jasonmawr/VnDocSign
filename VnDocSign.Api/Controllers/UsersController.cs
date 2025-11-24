using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Common;
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

    // GET ALL USERS
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var data = await _svc.GetAllAsync(ct);
        return Ok(ApiResponse.SuccessResponse(data));
    }

    // CREATE
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserCreateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest req, CancellationToken ct)
    {
        var data = await _svc.CreateAsync(req, ct);
        return Ok(ApiResponse.SuccessResponse(data, "Tạo tài khoản thành công."));
    }

    // GET ROLES OF A USER
    [HttpGet("{userId:guid}/roles")]
    [ProducesResponseType(typeof(ApiResponse<UserWithRolesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(Guid userId, CancellationToken ct)
    {
        var data = await _svc.GetWithRolesAsync(userId, ct);
        return Ok(ApiResponse.SuccessResponse(data));
    }

    // ASSIGN ROLES
    [HttpPost("{userId:guid}/roles")]
    [ProducesResponseType(typeof(ApiResponse<UserWithRolesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRoles(Guid userId, [FromBody] AssignRolesRequest req, CancellationToken ct)
    {
        var data = await _svc.AssignRolesAsync(userId, req, ct);
        return Ok(ApiResponse.SuccessResponse(data, "Gán role thành công."));
    }

    // REMOVE ROLES
    [HttpDelete("{userId:guid}/roles")]
    [ProducesResponseType(typeof(ApiResponse<UserWithRolesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveRoles(Guid userId, [FromBody] AssignRolesRequest req, CancellationToken ct)
    {
        var data = await _svc.RemoveRolesAsync(userId, req, ct);
        return Ok(ApiResponse.SuccessResponse(data, "Gỡ role thành công."));
    }
}
