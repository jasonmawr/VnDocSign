using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Interfaces.Signatures;
using VnDocSign.Application.Contracts.Dtos.Signatures;

namespace VnDocSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/signatures")]
public sealed class SignaturesController : ControllerBase
{
    private readonly ISignatureService _svc;
    public SignaturesController(ISignatureService svc) => _svc = svc;

    /// <summary>
    /// Upload ảnh chữ ký tay (image) cho 1 user.
    /// </summary>
    [HttpPost("{userId:guid}/upload")]
    [RequestSizeLimit(10_000_000)] // 10MB
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<UploadSignatureResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        Guid userId,
        IFormFile file,   // KHÔNG dùng [FromForm] ở đây
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.FailResponse("Empty file."));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var req = new UploadSignatureRequest(
            userId,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            ms.ToArray()
        );

        var res = await _svc.UploadAsync(req, ct);
        return Ok(ApiResponse<UploadSignatureResponse>.SuccessResponse(
            res,
            "Upload chữ ký tay thành công."));
    }

    /// <summary>
    /// Lấy ảnh chữ ký tay của user (trả trực tiếp file image).
    /// </summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var res = await _svc.GetAsync(new GetSignatureQuery(userId), ct);
        if (res is null)
            return NotFound(ApiResponse.FailResponse("Không tìm thấy chữ ký của người dùng."));

        return File(res.Data, res.ContentType, res.FileName);
    }
}
