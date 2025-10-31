using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using VnDocSign.Application.Contracts.Interfaces.Signatures;
using VnDocSign.Application.Contracts.Dtos.Signatures;

[Authorize]
[ApiController]
[Route("api/signatures")]
public sealed class SignaturesController : ControllerBase
{
    private readonly ISignatureService _svc;
    public SignaturesController(ISignatureService svc) => _svc = svc;

    [HttpPost("{userId:guid}/upload")]
    [RequestSizeLimit(10_000_000)] // 10MB
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadSignatureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadSignatureResponse>> Upload(
        Guid userId,
        IFormFile file,                 // <-- KHÔNG dùng [FromForm] ở đây
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Empty file.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var req = new UploadSignatureRequest(
            userId,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            ms.ToArray()
        );

        var res = await _svc.UploadAsync(req, ct);
        return Ok(res);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var res = await _svc.GetAsync(new GetSignatureQuery(userId), ct);
        return res is null ? NotFound() : File(res.Data, res.ContentType, res.FileName);
    }
}
