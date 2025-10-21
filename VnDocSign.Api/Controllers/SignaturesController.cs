using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Contracts.Dtos.Signatures;
using VnDocSign.Application.Contracts.Interfaces.Signatures;

namespace VnDocSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/signatures")]
public sealed class SignaturesController : ControllerBase
{
    private readonly ISignatureService _svc;
    public SignaturesController(ISignatureService svc) => _svc = svc;

    [HttpPost("{userId:guid}")]
    public async Task<IActionResult> Upload(Guid userId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("Empty file.");
        using var ms = new MemoryStream(); await file.CopyToAsync(ms, ct);
        var res = await _svc.UploadAsync(new UploadSignatureRequest(userId, file.FileName, file.ContentType, ms.ToArray()), ct);
        return Ok(res);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var res = await _svc.GetAsync(new GetSignatureQuery(userId), ct);
        return res is null ? NotFound() : File(res.Data, res.ContentType, res.FileName);
    }
}
