using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        public async Task<ActionResult<TemplateUploadResultDto>> Upload(
            [FromForm] string TemplateCode, [FromForm] string? Name,
            [FromForm] string? Notes, [FromForm] IFormFile file,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var uid = Guid.TryParse(userId, out var g) ? g : (Guid?)null;

            var meta = new TemplateUploadRequest(TemplateCode, Name, Notes);
            var result = await _svc.UploadAsync(meta, file, uid, ct);
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
