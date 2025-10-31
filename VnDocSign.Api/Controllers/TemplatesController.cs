using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VnDocSign.Api.Models;
using VnDocSign.Application.Contracts.Dtos.Templates;
using VnDocSign.Application.Contracts.Interfaces.Documents;

namespace VnDocSign.Api.Controllers
{
    [ApiController]
    [Route("api/templates")]
    [Authorize(Policy = "RequireAdmin")] // hoặc policy riêng như “TemplateAdmin” nếu bạn tạo role
    public class TemplatesController : ControllerBase
    {
        private readonly ITemplateService _svc;
        public TemplatesController(ITemplateService svc) { _svc = svc; }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TemplateListItemDto>>> GetAll(CancellationToken ct)
            => Ok(await _svc.GetAllAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TemplateDetailDto>> Get(int id, CancellationToken ct)
            => Ok(await _svc.GetDetailAsync(id, ct));

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] // 100MB
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(TemplateUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TemplateUploadResultDto>> Upload(
            [FromForm] TemplateUploadForm form,
            CancellationToken ct)
        {
            // Lấy user Id từ token (nếu có)
            var userIdStr = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? userId = Guid.TryParse(userIdStr, out var g) ? g : (Guid?)null;

            // Map sang request của Application layer
            var req = new TemplateUploadRequest(
                form.TemplateCode,
                form.Name,
                form.Notes
            );

            // Gọi service lưu metadata + file
            var result = await _svc.UploadAsync(req, form.File, userId, ct);
            return Ok(result);
        }

        [HttpPatch("{id:int}/toggle")]
        public async Task<IActionResult> Toggle(int id, CancellationToken ct)
        { await _svc.ToggleActiveAsync(id, ct); return NoContent(); }

        [HttpDelete("versions/{versionId:int}")]
        public async Task<IActionResult> DeleteVersion(int versionId, CancellationToken ct)
        { await _svc.DeleteVersionAsync(versionId, ct); return NoContent(); }
    }
}
