using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Dossiers;
using VnDocSign.Application.Contracts.Dtos.Signing;
using VnDocSign.Application.Contracts.Interfaces.Dossiers;
using VnDocSign.Application.Contracts.Interfaces.Integration;

namespace VnDocSign.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/dossiers")]
    public sealed class DossiersController : ControllerBase
    {
        private readonly IDossierService _dossierService;
        private readonly IDossierContentService _contentService;
        private readonly IPdfRenderService _pdf;

        public DossiersController(
            IDossierService dossierService,
            IDossierContentService contentService,
            IPdfRenderService pdf)
        {
            _dossierService = dossierService;
            _contentService = contentService;
            _pdf = pdf;
        }

        // ===== Helpers =====

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return null;
            return Guid.TryParse(claim.Value, out var g) ? g : (Guid?)null;
        }

        private Guid GetRequiredUserId()
        {
            var id = GetCurrentUserId();
            if (id is null)
                throw new UnauthorizedAccessException("Cannot determine current user id from token.");
            return id.Value;
        }

        // ===== API =====

        /// <summary>
        /// Tạo mới hồ sơ (Dossier) + các SignTask vùng 1 (Người trình, LĐ phòng).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<DossierCreateResponse>), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] DossierCreateRequest req, CancellationToken ct)
        {
            var res = await _dossierService.CreateAsync(req, ct);
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<DossierCreateResponse>.SuccessResponse(res, "Tạo hồ sơ thành công."));
        }

        /// <summary>
        /// Danh sách hồ sơ do user hiện tại tạo (“Hồ sơ của tôi”).
        /// </summary>
        [HttpGet("my")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DossierListItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyDossiers(CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized(ApiResponse<IReadOnlyList<DossierListItemDto>>
                    .Fail("Không xác định được user hiện tại từ."));
            }

            var items = await _dossierService.GetMyCreatedAsync(userId.Value, ct);
            return Ok(ApiResponse<IReadOnlyList<DossierListItemDto>>.SuccessResponse(items));
        }

        /// <summary>
        /// Lấy nội dung hiện tại của hồ sơ (data các trường phiếu).
        /// </summary>
        [HttpGet("{id:guid}/content")]
        [ProducesResponseType(typeof(ApiResponse<DossierContentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetContent(Guid id, CancellationToken ct)
        {
            var dto = await _contentService.GetAsync(id, ct);
            return Ok(ApiResponse<DossierContentDto>.SuccessResponse(dto));
        }

        /// <summary>
        /// Cập nhật nội dung hồ sơ (data phiếu trình).
        /// </summary>
        [HttpPut("{id:guid}/content")]
        [ProducesResponseType(typeof(ApiResponse<DossierContentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpsertContent(
            Guid id,
            [FromBody] DossierContentUpsertRequest req,
            CancellationToken ct)
        {
            var userId = GetCurrentUserId();
            var dto = await _contentService.UpsertAsync(id, req, userId, ct);
            return Ok(ApiResponse<DossierContentDto>.SuccessResponse(dto, "Cập nhật nội dung hồ sơ thành công."));
        }

        /// <summary>
        /// Cập nhật tuyến trình ký (phòng liên quan + các phòng chức năng).
        /// </summary>
        [HttpPost("{id:guid}/route")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Route(
            Guid id,
            [FromBody] DossierRouteRequest req,
            CancellationToken ct)
        {
            var slots = req.SelectedFunctionalSlots ?? Array.Empty<string>();
            await _dossierService.RouteAsync(id, req.RelatedDepartmentId, slots, ct);
            return Ok(ApiResponse<string>.SuccessResponse("Cập nhật tuyến trình ký thành công."));
        }

        /// <summary>
        /// Render hồ sơ ra PDF từ template + dữ liệu hiện tại.
        /// Phiếu trình chưa ký (Source.pdf).
        /// </summary>
        [HttpPost("{id:guid}/render")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> Render(Guid id, CancellationToken ct)
        {
            var userId = GetRequiredUserId();
            await _dossierService.EnsureCanRenderAsync(id, userId, ct);

            var pdfPath = await _pdf.RenderDossierToPdf(id, ct);
            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath, ct);

            return File(bytes, "application/pdf", "Dossier.pdf");
        }

        /// <summary>
        /// Render hồ sơ đã ký MOCK ra PDF (Signed_vN.pdf):
        /// - Dùng Source.docx làm nền
        /// - Chèn PNG chữ ký các SignTask đã Approved
        /// </summary>
        [HttpPost("{id:guid}/render-signed")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> RenderSigned(Guid id, CancellationToken ct)
        {
            var userId = GetRequiredUserId();
            await _dossierService.EnsureCanRenderAsync(id, userId, ct);

            var pdfPath = await _pdf.RenderSignedMockAsync(id, ct);
            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath, ct);

            return File(bytes, "application/pdf", "Dossier_Signed.pdf");
        }
    }

    // Request body cho /route
    public sealed record DossierRouteRequest(
        Guid? RelatedDepartmentId,
        IReadOnlyCollection<string>? SelectedFunctionalSlots);
}
