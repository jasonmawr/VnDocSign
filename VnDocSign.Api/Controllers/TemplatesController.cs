using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VnDocSign.Application.Common;
using VnDocSign.Api.Models;
using VnDocSign.Application.Contracts.Dtos.Templates;
using VnDocSign.Application.Contracts.Interfaces.Documents;

namespace VnDocSign.Api.Controllers
{
    [ApiController]
    [Route("api/templates")]
    [Authorize(Policy = "RequireAdmin")] // hoặc policy riêng như “TemplateAdmin” nếu sau này bạn tách
    public sealed class TemplatesController : ControllerBase
    {
        private readonly ITemplateService _svc;
        public TemplatesController(ITemplateService svc) { _svc = svc; }

        /// <summary>
        /// Lấy danh sách template cùng version mới nhất (nếu có).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TemplateListItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var data = await _svc.GetAllAsync(ct);
            return Ok(ApiResponse<IReadOnlyList<TemplateListItemDto>>.SuccessResponse(data));
        }

        /// <summary>
        /// Lấy chi tiết template + toàn bộ các version.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<TemplateDetailDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            var dto = await _svc.GetDetailAsync(id, ct);
            return Ok(ApiResponse<TemplateDetailDto>.SuccessResponse(dto));
        }

        /// <summary>
        /// Upload file DOCX làm version mới cho template.
        /// Nếu TemplateCode chưa tồn tại sẽ tự tạo template mới.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] // 100MB
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<TemplateUploadResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Upload(
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

            var result = await _svc.UploadAsync(req, form.File, userId, ct);
            return Ok(ApiResponse<TemplateUploadResultDto>.SuccessResponse(
                result,
                "Upload template thành công."));
        }

        /// <summary>
        /// Bật/tắt trạng thái hoạt động của template.
        /// </summary>
        [HttpPatch("{id:int}/toggle")]
        [ProducesResponseType(typeof(ApiResponse<TemplateListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Toggle(int id, CancellationToken ct)
        {
            var dto = await _svc.ToggleActiveAsync(id, ct);
            var msg = dto.IsActive ? "Đã kích hoạt template." : "Đã vô hiệu hóa template.";
            return Ok(ApiResponse<TemplateListItemDto>.SuccessResponse(dto, msg));
        }

        /// <summary>
        /// Xoá 1 version của template (record version).
        /// File vật lý có thể được giữ lại để backup (tuỳ chiến lược).
        /// </summary>
        [HttpDelete("versions/{versionId:int}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteVersion(int versionId, CancellationToken ct)
        {
            await _svc.DeleteVersionAsync(versionId, ct);
            return Ok(ApiResponse<string>.SuccessResponse("Xoá version template thành công."));
        }
    }
}
