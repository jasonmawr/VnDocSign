using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("my-tasks")]
    public Task<IReadOnlyList<MyTaskItem>> MyTasks([FromQuery] Guid userId, CancellationToken ct)
        => _svc.GetMyTasksAsync(userId, ct);

    [HttpPost("{taskId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid taskId, [FromBody] ApproveBody body, CancellationToken ct)
    {
        await _svc.ApproveAndSignAsync(new ApproveRequest(taskId, body.ActorUserId, body.Pin, body.Comment), ct);
        return Ok();
    }

    [HttpPost("{taskId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid taskId, [FromBody] RejectBody body, CancellationToken ct)
    {
        await _svc.RejectAsync(new RejectRequest(taskId, body.ActorUserId, body.Comment), ct);
        return Ok();
    }

    [HttpPost("{dossierId:guid}/clerks/confirm")]
    public async Task<IActionResult> ClerkConfirm(Guid dossierId, [FromBody] ClerkConfirmBody body, CancellationToken ct)
    {
        await _svc.ClerkConfirmAsync(new ClerkConfirmRequest(dossierId, body.ActorUserId), ct);
        return Ok();
    }

    public sealed record ApproveBody(Guid ActorUserId, string Pin, string? Comment);
    public sealed record RejectBody(Guid ActorUserId, string? Comment);
    public sealed record ClerkConfirmBody(Guid ActorUserId);
}
