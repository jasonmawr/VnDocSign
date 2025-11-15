using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;                                      // GĐ4: dùng để đọc DataJson
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnDocSign.Application.Contracts.Interfaces.Integration;
using VnDocSign.Domain.Entities.Dossiers;
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Infrastructure.Documents.Templates;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Infrastructure.Setup.Options;

namespace VnDocSign.Infrastructure.Documents
{
    public sealed class PdfRenderService : IPdfRenderService
    {
        private readonly AppDbContext _db;
        private readonly IPdfConverter _pdfConverter;
        private readonly ILogger<PdfRenderService> _logger;
        private readonly string _root;

        // Template defaults
        private const string DefaultTemplateFile = "PhieuTrinhTemplate.docx";
        private static readonly string TemplatesRoot =
            Path.Combine("VnDocSign.Infrastructure", "Documents", "Templates");

        public PdfRenderService(
            AppDbContext db,
            IPdfConverter pdfConverter,
            IOptions<FileStorageOptions> fsOptions,
            ILogger<PdfRenderService> logger)
        {
            _db = db;
            _pdfConverter = pdfConverter;
            _logger = logger;
            _root = fsOptions?.Value?.Root ?? "./data";
        }

        public async Task<string> RenderDossierToPdf(Guid dossierId, CancellationToken ct = default)
        {
            var (_, pdf) = await RenderCoreAsync(dossierId, ct);
            return pdf;
        }

        public Task<string> EnsurePdf(string anyPath, CancellationToken ct = default)
        {
            if (anyPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Path.GetFullPath(anyPath));

            if (anyPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return _pdfConverter.ConvertDocxToPdfAsync(anyPath, ct);

            throw new NotSupportedException("Only DOCX→PDF is supported in stage 1.");
        }

        // ====================== Core ======================
        // ====================== Core ======================
        private async Task<(string DocxPath, string PdfPath)> RenderCoreAsync(Guid dossierId, CancellationToken ct)
        {
            // 1) Tải đầy đủ Dossier + CreatedBy.Department + SignTasks
            var dossier = await _db.Dossiers
                .Include(x => x.CreatedBy)
                    .ThenInclude(u => u!.Department)
                .Include(x => x.SignTasks)
                    .ThenInclude(t => t.Assignee)
                        .ThenInclude(u => u!.Department)
                .FirstOrDefaultAsync(x => x.Id == dossierId, ct)
                ?? throw new InvalidOperationException($"Dossier {dossierId} not found");

            // 2) Thư mục output của hồ sơ
            var dossierDir = Path.Combine(Path.GetFullPath(_root), "dossiers", dossierId.ToString("N"));
            Directory.CreateDirectory(dossierDir);
            var srcDocx = Path.Combine(dossierDir, "Source.docx");
            var srcPdf = Path.Combine(dossierDir, "Source.pdf");

            // 3) Lấy DossierContent & TemplateCode
            var dc = await _db.DossierContents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DossierId == dossierId, ct);

            if (dc == null || string.IsNullOrWhiteSpace(dc.TemplateCode))
                throw new InvalidOperationException("Dossier has no TemplateCode. Please set content with a valid TemplateCode before rendering.");

            // 4) Tìm TemplateVersion mới nhất theo TemplateCode và lấy đường dẫn DOCX đã upload
            var tpl = await _db.Templates
                .Include(t => t.Versions.OrderByDescending(v => v.VersionNo))
                .FirstOrDefaultAsync(t => t.Code == dc.TemplateCode, ct);

            if (tpl == null || tpl.Versions.Count == 0)
                throw new FileNotFoundException($"No template version found for code: {dc.TemplateCode}");

            var latest = tpl.Versions.First(); // VersionNo lớn nhất do đã OrderByDescending
                                               // FileNameDocx là path tương đối kiểu "templates/{templateId}/v{version}/Source.docx"
            var templateDocxPath = Path.Combine(Path.GetFullPath(_root), latest.FileNameDocx);

            if (!File.Exists(templateDocxPath))
                throw new FileNotFoundException("Template DOCX not found.", templateDocxPath);

            // 5) Copy file khuôn về thư mục hồ sơ để điền dữ liệu
            File.Copy(templateDocxPath, srcDocx, overwrite: true);

            // 6) Chuẩn bị map alias (mặc định + ghi đè từ DataJson nếu có)
            var aliasFromDb = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (dc is not null && !string.IsNullOrWhiteSpace(dc.DataJson))
            {
                try
                {
                    aliasFromDb = JsonSerializer.Deserialize<Dictionary<string, string?>>(dc.DataJson)
                                  ?? new Dictionary<string, string?>();
                }
                catch
                {
                    // ignore malformed json
                }
            }

            var mapRichText = new Dictionary<string, string?>
            {
                ["VU_VIEC"] = dossier.Title ?? string.Empty,
                ["DON_VI"] = dossier.CreatedBy?.Department?.Name ?? string.Empty,
                ["KINH_GUI"] = "Ban Giám đốc", // bạn tùy chỉnh nếu cần
                ["CANCU_PHAPLY"] = string.Empty,
                ["NOIDUNG_TRINH"] = string.Empty,
                ["KIENNGHI_DEXUAT"] = string.Empty,

                // Nhóm 1
                ["NGUOITRINH_HOTEN"] = GetAssigneeName(dossier!, SlotKey.NguoiTrinh),
                ["LANHDAO_HOTEN"] = GetAssigneeName(dossier!, SlotKey.LanhDaoPhong),
                ["PHONGBAN_LIENQUAN"] = GetAssigneeDept(dossier!, SlotKey.DonViLienQuan),
                ["GHICHUPHONG_LIENQUAN"] = GetComment(dossier!, SlotKey.DonViLienQuan),
                ["HOTEN_LIENQUAN"] = GetAssigneeName(dossier!, SlotKey.DonViLienQuan),

                // Nhóm 2
                ["KHTH_SIGN_NAME"] = GetAssigneeName(dossier!, SlotKey.KHTH),
                ["GHICHU_KHTH"] = GetComment(dossier!, SlotKey.KHTH),
                ["HCQT_SIGN_NAME"] = GetAssigneeName(dossier!, SlotKey.HCQT),
                ["GHICHU_HCQT"] = GetComment(dossier!, SlotKey.HCQT),
                ["TCCB_SIGN_NAME"] = GetAssigneeName(dossier!, SlotKey.TCCB),
                ["GHICHU_TCCB"] = GetComment(dossier!, SlotKey.TCCB),
                ["TCKT_SIGN_NAME"] = GetAssigneeName(dossier!, SlotKey.TCKT),
                ["GHICHU_TCKT"] = GetComment(dossier!, SlotKey.TCKT),
                ["CTCD_SIGN_NAME"] = GetAssigneeName(dossier!, SlotKey.CTCD),
                ["GHICHU_CTCD"] = GetComment(dossier!, SlotKey.CTCD),

                // Nhóm 3
                ["PGD_NAME_1"] = GetAssigneeName(dossier!, SlotKey.PGD1),
                ["GHICHU_PGD_1"] = GetComment(dossier!, SlotKey.PGD1),
                ["PGD_NAME_2"] = GetAssigneeName(dossier!, SlotKey.PGD2),
                ["GHICHU_PGD_2"] = GetComment(dossier!, SlotKey.PGD2),
                ["PGD_NAME_3"] = GetAssigneeName(dossier!, SlotKey.PGD3),
                ["GHICHU_PGD_3"] = GetComment(dossier!, SlotKey.PGD3),

                // Nhóm 4
                ["GD_NAME"] = GetAssigneeName(dossier!, SlotKey.GiamDoc),
                ["GHICHU_GD"] = GetComment(dossier!, SlotKey.GiamDoc),
            };

            // Ghi đè alias từ DB (nếu có)
            foreach (var kv in aliasFromDb)
                mapRichText[kv.Key] = kv.Value ?? string.Empty;

            // 7) Ẩn các BLOCK_* nếu thiếu người/không dùng
            var blocksToHide = new List<string>();
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.DonViLienQuan))) blocksToHide.Add("BLOCK_LIENQUAN");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.KHTH))) blocksToHide.Add("BLOCK_KHTH");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.HCQT))) blocksToHide.Add("BLOCK_HCQT");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.TCCB))) blocksToHide.Add("BLOCK_TCCB");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.TCKT))) blocksToHide.Add("BLOCK_TCKT");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.CTCD))) blocksToHide.Add("BLOCK_CTCD");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.PGD1))) blocksToHide.Add("BLOCK_PGD_1");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.PGD2))) blocksToHide.Add("BLOCK_PGD_2");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.PGD3))) blocksToHide.Add("BLOCK_PGD_3");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier!, SlotKey.GiamDoc))) blocksToHide.Add("BLOCK_GD");

            // 8) Điền alias + ẩn BLOCK_* (KHÔNG chèn ảnh chữ ký vào Source)
            using (var doc = WordprocessingDocument.Open(srcDocx, true))
            {
                // 8.1) Fill toàn bộ alias text từ mapRichText (DataJson + SignTask)
                foreach (var kv in mapRichText)
                    OpenXmlTemplateEngine.FillRichTextByAlias(doc, kv.Key, kv.Value ?? string.Empty);

                // 8.2) KHÔNG chèn ảnh chữ ký vào Source
                // Mock ký (PNG) và ký số SSM sẽ xử lý ở bước khác (Signed_vN.pdf),
                // không dùng cho Source.pdf.

                // 8.3) Xóa các BLOCK_* tương ứng slot không dùng/không có người
                foreach (var b in blocksToHide.Distinct(StringComparer.OrdinalIgnoreCase))
                    OpenXmlTemplateEngine.RemoveBlockByAlias(doc, b);
            }

            // 9) Convert DOCX -> PDF
            var outPdf = await _pdfConverter.ConvertDocxToPdfAsync(srcDocx, ct);
            if (!File.Exists(outPdf))
                throw new InvalidOperationException("Failed to convert PDF");

            if (!string.Equals(Path.GetFullPath(outPdf), Path.GetFullPath(srcPdf), StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(srcPdf)) File.Delete(srcPdf);
                File.Move(outPdf, srcPdf);
            }

            return (srcDocx, srcPdf);
        }

        /// <summary>
        /// Tạo bản Signed_vN.docx/pdf cho ký MOCK:
        /// - Đảm bảo đã có Source.docx (gọi lại RenderCoreAsync nếu cần).
        /// - Copy Source.docx -> Signed_vN.docx (N là số lớn nhất + 1).
        /// - Chèn PNG chữ ký (UserSignatures) vào các alias SIG_* tương ứng
        ///   với các SignTask đã Approved.
        /// - Convert -> Signed_vN.pdf và trả về đường dẫn tuyệt đối.
        /// </summary>
        public async Task<string> RenderSignedMockAsync(Guid dossierId, CancellationToken ct = default)
        {
            // 1) Đảm bảo đã render xong Source.docx/Source.pdf
            var (srcDocx, _) = await RenderCoreAsync(dossierId, ct);
            var dossierDir = Path.GetDirectoryName(srcDocx)
                ?? throw new InvalidOperationException("Cannot determine dossier directory.");

            // 2) Tìm version N tiếp theo cho Signed_vN
            var existingSigned = Directory.GetFiles(dossierDir, "Signed_v*.pdf");
            var maxVersion = 0;
            foreach (var path in existingSigned)
            {
                var fileName = Path.GetFileNameWithoutExtension(path); // vd: "Signed_v3"
                var idx = fileName.LastIndexOf('v');
                if (idx >= 0 && int.TryParse(fileName[(idx + 1)..], out var n) && n > maxVersion)
                    maxVersion = n;
            }
            var version = maxVersion + 1;

            var signedDocx = Path.Combine(dossierDir, $"Signed_v{version}.docx");
            var signedPdf = Path.Combine(dossierDir, $"Signed_v{version}.pdf");

            // 3) Copy Source.docx -> Signed_vN.docx (overwrite nếu tồn tại)
            File.Copy(srcDocx, signedDocx, overwrite: true);

            // 4) Load lại Dossier + SignTasks để biết ai đã ký (Approved)
            var dossier = await _db.Dossiers
                .Include(x => x.SignTasks)
                .FirstOrDefaultAsync(x => x.Id == dossierId, ct)
                ?? throw new InvalidOperationException($"Dossier {dossierId} not found");

            // 5) Map SlotKey -> alias Picture trong DOCX (theo tài liệu phiếu trình)
            var imageAliasBySlot = new Dictionary<SlotKey, string>
            {
                [SlotKey.NguoiTrinh] = "SIG_NGUOITRINH_IMAGE",
                [SlotKey.LanhDaoPhong] = "SIG_LANHDAO_IMAGE",
                [SlotKey.DonViLienQuan] = "SIG_LIENQUAN_IMAGE",
                [SlotKey.KHTH] = "KHTH_SIGN_IMAGE",
                [SlotKey.HCQT] = "HCQT_SIGN_IMAGE",
                [SlotKey.TCCB] = "TCCB_SIGN_IMAGE",
                [SlotKey.TCKT] = "TCKT_SIGN_IMAGE",
                [SlotKey.CTCD] = "CTCD_SIGN_IMAGE",
                [SlotKey.PGD1] = "PGD1_SIGN_IMAGE",
                [SlotKey.PGD2] = "PGD2_SIGN_IMAGE",
                [SlotKey.PGD3] = "PGD3_SIGN_IMAGE",
                [SlotKey.GiamDoc] = "GD_SIGN_IMAGE"
            };

            // 6) Lấy PNG chữ ký cho các slot đã Approved
            var pngByAlias = new Dictionary<string, byte[]>();
            foreach (var kv in imageAliasBySlot)
            {
                var slotKey = kv.Key;
                var alias = kv.Value;

                var task = dossier.SignTasks?
                    .FirstOrDefault(t => t.SlotKey == slotKey && t.Status == SignTaskStatus.Approved);

                if (task == null) continue;

                // Lấy png từ UserSignatures (hàm helper sẵn có)
                var png = await TryGetSignaturePngAsync(dossier, slotKey, ct);
                if (png != null && png.Length > 0)
                {
                    pngByAlias[alias] = png;
                }
            }

            // 7) Mở Signed_vN.docx và chèn ảnh vào các alias tương ứng
            if (pngByAlias.Count > 0)
            {
                using (var doc = WordprocessingDocument.Open(signedDocx, true))
                {
                    foreach (var kv in pngByAlias)
                    {
                        OpenXmlTemplateEngine.SetImageByAlias(doc, kv.Key, kv.Value);
                    }
                }
            }

            // 8) Convert DOCX -> PDF
            var outPdf = await _pdfConverter.ConvertDocxToPdfAsync(signedDocx, ct);
            if (!File.Exists(outPdf))
                throw new InvalidOperationException("Failed to convert signed PDF");

            if (!string.Equals(Path.GetFullPath(outPdf), Path.GetFullPath(signedPdf), StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(signedPdf)) File.Delete(signedPdf);
                File.Move(outPdf, signedPdf);
            }

            return signedPdf;
        }

        // ====================== Helpers ======================
        private static SignTask? FindTask(Dossier? dossier, SlotKey key)
            => dossier?.SignTasks?.FirstOrDefault(t => t.SlotKey == key);

        private static string? GetAssigneeName(Dossier? dossier, SlotKey key)
        {
            var task = FindTask(dossier, key);
            var name = task?.Assignee?.FullName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static string? GetAssigneeDept(Dossier? dossier, SlotKey key)
            => FindTask(dossier, key)?.Assignee?.Department?.Name;

        private static string? GetComment(Dossier? dossier, SlotKey key)
            => FindTask(dossier, key)?.Comment;

        private async Task<byte[]?> TryGetSignaturePngAsync(Dossier? dossier, SlotKey key, CancellationToken ct)
        {
            var task = FindTask(dossier, key);
            if (task == null || task.AssigneeId == Guid.Empty) return null;

            var sig = await _db.UserSignatures
                .Where(s => s.UserId == task.AssigneeId)
                .OrderByDescending(s => s.UploadedAt)
                .FirstOrDefaultAsync(ct);

            return sig?.Data; // byte[] PNG
        }
    }
}
