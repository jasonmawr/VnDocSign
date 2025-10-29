using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VnDocSign.Application.Contracts.Dtos.Dossiers;
using VnDocSign.Application.Contracts.Interfaces;                 // IDossierService
using VnDocSign.Application.Contracts.Interfaces.Dossiers;        // IDossierContentService
using VnDocSign.Application.Contracts.Interfaces.Integration;     // IPdfRenderService

namespace VnDocSign.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/dossiers")]
    public sealed class DossiersController : ControllerBase
    {
        private readonly IDossierService _svc;
        private readonly IDossierContentService _contentSvc;
        private readonly IPdfRenderService _pdf;

        public DossiersController(
            IDossierService svc,
            IDossierContentService contentSvc,
            IPdfRenderService pdf)
        {
            _svc = svc;
            _contentSvc = contentSvc;
            _pdf = pdf;
        }

        // === GĐ2: tạo hồ sơ như cũ ===
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DossierCreateRequest req, CancellationToken ct)
            => Ok(await _svc.CreateAsync(req, ct));

        // === GĐ4: lấy nội dung phiếu ===
        [HttpGet("{id:guid}/content")]
        public async Task<ActionResult<DossierContentDto>> GetContent(Guid id, CancellationToken ct)
            => Ok(await _contentSvc.GetAsync(id, ct));

        // === GĐ4: lưu/sửa nội dung phiếu ===
        [HttpPut("{id:guid}/content")]
        public async Task<ActionResult<DossierContentDto>> UpsertContent(Guid id, [FromBody] DossierContentUpsertRequest req, CancellationToken ct)
        {
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? uid = Guid.TryParse(uidStr, out var g) ? g : (Guid?)null;
            var dto = await _contentSvc.UpsertAsync(id, req, uid, ct);
            return Ok(dto);
        }

        // === GĐ4: render PDF theo nội dung đã lưu ===
        [HttpPost("{id:guid}/render")]
        public async Task<IActionResult> Render(Guid id, CancellationToken ct)
        {
            var pdfPath = await _pdf.RenderDossierToPdf(id, ct);
            var bytes = await System.IO.File.ReadAllBytesAsync(pdfPath, ct);
            return File(bytes, "application/pdf", "Dossier.pdf");
        }
    }
}
