using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Signing;
using VnDocSign.Application.Contracts.Interfaces.Signing;

namespace VnDocSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/signing")]
public sealed class SigningController : ControllerBase
{
    private readonly ISigningService _svc;
    public SigningController(ISigningService svc) => _svc = svc;

    // Lấy userId từ JWT: ưu tiên claim "uid", fallback NameIdentifier
    private Guid GetCurrentUserId()
    {
        var uidStr = User.FindFirst("uid")?.Value
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(uidStr, out var uid))
            throw new UnauthorizedAccessException("Invalid or missing user id in token.");

        return uid;
    }

    // === Danh sách task của chính user đang đăng nhập ===
    [HttpGet("my-tasks")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MyTaskItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyTasks(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var data = await _svc.GetMyTasksAsync(userId, ct);
        return Ok(ApiResponse<IReadOnlyList<MyTaskItem>>.SuccessResponse(data));
    }

    // === Approve + ký (Mock hoặc SSM tùy Pin & cấu hình Ssm:Mode) ===
    [HttpPost("{taskId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid taskId, [FromBody] ApproveBody body, CancellationToken ct)
    {
        var actorUserId = GetCurrentUserId();

        await _svc.ApproveAndSignAsync(
            new ApproveRequest(taskId, actorUserId, body.Pin ?? string.Empty, body.Comment),
            ct);

        return Ok(ApiResponse.SuccessResponse("Phê duyệt và ký thành công."));
    }

    // === Reject task ===
    [HttpPost("{taskId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid taskId, [FromBody] RejectBody body, CancellationToken ct)
    {
        var actorUserId = GetCurrentUserId();

        await _svc.RejectAsync(
            new RejectRequest(taskId, actorUserId, body.Comment),
            ct);

        return Ok(ApiResponse.SuccessResponse("Từ chối hồ sơ thành công."));
    }

    // === Văn thư xác nhận (mở bước Giám đốc) ===
    [HttpPost("{dossierId:guid}/clerks/confirm")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClerkConfirm(Guid dossierId, CancellationToken ct)
    {
        var actorUserId = GetCurrentUserId();

        await _svc.ClerkConfirmAsync(
            new ClerkConfirmRequest(dossierId, actorUserId),
            ct);

        return Ok(ApiResponse.SuccessResponse("Văn thư đã xác nhận, chuyển bước Giám đốc ký."));
    }

    // === My tasks (grouped) ===
    [HttpGet("my-tasks/grouped")]
    [ProducesResponseType(typeof(ApiResponse<MyTasksGroupedDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTasksGrouped(CancellationToken ct)
    {
        var actorUserId = GetCurrentUserId();
        var data = await _svc.GetMyTasksGroupedAsync(actorUserId, ct);
        return Ok(ApiResponse<MyTasksGroupedDto>.SuccessResponse(data));
    }

    // ====== Request bodies ======
    public sealed record RejectBody(string? Comment);
}
