// ===============================
// FILE: VnDocSign.Infrastructure/Documents/PdfRenderService.cs
// ===============================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private async Task<(string DocxPath, string PdfPath)> RenderCoreAsync(Guid dossierId, CancellationToken ct)
        {
            var dossier = await _db.Dossiers
                .Include(x => x.SignTasks).ThenInclude(t => t.Assignee).ThenInclude(u => u.Department)
                .FirstOrDefaultAsync(x => x.Id == dossierId, ct)
                ?? throw new InvalidOperationException($"Dossier {dossierId} not found");

            var dossierDir = Path.Combine(Path.GetFullPath(_root), "dossiers", dossierId.ToString("N"));
            Directory.CreateDirectory(dossierDir);
            var srcDocx = Path.Combine(dossierDir, "Source.docx");
            var srcPdf = Path.Combine(dossierDir, "Source.pdf");

            // Resolve template
            var templatePath = Path.Combine(TemplatesRoot, DefaultTemplateFile);
            if (!File.Exists(templatePath))
                templatePath = Path.Combine(AppContext.BaseDirectory, TemplatesRoot, DefaultTemplateFile);
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Missing template", templatePath);

            File.Copy(templatePath, srcDocx, overwrite: true);

            // ==== Build mapping (tối thiểu GĐ1) ====
            var mapRichText = new Dictionary<string, string?>
            {
                ["VU_VIEC"] = dossier.Title,
                ["DON_VI"] = dossier?.CreatedBy?.Department?.Name,
                ["KINH_GUI"] = "Ban Giám đốc Viện Y Dược học Dân tộc",
                ["CANCU_PHAPLY"] = string.Empty,
                ["NOIDUNG_TRINH"] = string.Empty,
                ["KIENNGHI_DEXUAT"] = string.Empty,

                // Nhóm 1
                ["NGUOITRINH_HOTEN"] = GetAssigneeName(dossier, SlotKey.NguoiTrinh),
                ["LANHDAO_HOTEN"] = GetAssigneeName(dossier, SlotKey.LanhDaoPhong),
                ["PHONGBAN_LIENQUAN"] = GetAssigneeDept(dossier, SlotKey.DonViLienQuan),
                ["GHICHUPHONG_LIENQUAN"] = GetComment(dossier, SlotKey.DonViLienQuan),
                ["HOTEN_LIENQUAN"] = GetAssigneeName(dossier, SlotKey.DonViLienQuan),

                // Nhóm 2
                ["KHTH_SIGN_NAME"] = GetAssigneeName(dossier, SlotKey.KHTH),
                ["GHICHU_KHTH"] = GetComment(dossier, SlotKey.KHTH),
                ["HCQT_SIGN_NAME"] = GetAssigneeName(dossier, SlotKey.HCQT),
                ["GHICHU_HCQT"] = GetComment(dossier, SlotKey.HCQT),
                ["TCCB_SIGN_NAME"] = GetAssigneeName(dossier, SlotKey.TCCB),
                ["GHICHU_TCCB"] = GetComment(dossier, SlotKey.TCCB),
                ["TCKT_SIGN_NAME"] = GetAssigneeName(dossier, SlotKey.TCKT),
                ["GHICHU_TCKT"] = GetComment(dossier, SlotKey.TCKT),
                ["CTCD_SIGN_NAME"] = GetAssigneeName(dossier, SlotKey.CTCD),
                ["GHICHU_CTCD"] = GetComment(dossier, SlotKey.CTCD),

                // Nhóm 3
                ["PGD_NAME_1"] = GetAssigneeName(dossier, SlotKey.PGD1),
                ["GHICHU_PGD_1"] = GetComment(dossier, SlotKey.PGD1),
                ["PGD_NAME_2"] = GetAssigneeName(dossier, SlotKey.PGD2),
                ["GHICHU_PGD_2"] = GetComment(dossier, SlotKey.PGD2),
                ["PGD_NAME_3"] = GetAssigneeName(dossier, SlotKey.PGD3),
                ["GHICHU_PGD_3"] = GetComment(dossier, SlotKey.PGD3),

                // Nhóm 4
                ["GD_NAME"] = GetAssigneeName(dossier, SlotKey.GiamDoc),
                ["GHICHU_GD"] = GetComment(dossier, SlotKey.GiamDoc),
            };

            var blocksToHide = new List<string>();
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.DonViLienQuan))) blocksToHide.Add("BLOCK_LIENQUAN");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.KHTH))) blocksToHide.Add("BLOCK_KHTH");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.HCQT))) blocksToHide.Add("BLOCK_HCQT");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.TCCB))) blocksToHide.Add("BLOCK_TCCB");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.TCKT))) blocksToHide.Add("BLOCK_TCKT");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.CTCD))) blocksToHide.Add("BLOCK_CTCD");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.PGD1))) blocksToHide.Add("BLOCK_PGD_1");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.PGD2))) blocksToHide.Add("BLOCK_PGD_2");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.PGD3))) blocksToHide.Add("BLOCK_PGD_3");
            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.GiamDoc))) blocksToHide.Add("BLOCK_GD");

            // ==== Merge vào DOCX ====
            using (var doc = WordprocessingDocument.Open(srcDocx, true))
            {
                // RichText
                foreach (var kv in mapRichText)
                    OpenXmlTemplateEngine.FillRichTextByAlias(doc, kv.Key, kv.Value ?? string.Empty);

                // Ảnh chữ ký (nếu có)
                foreach (var slot in new[] { SlotKey.NguoiTrinh, SlotKey.LanhDaoPhong, SlotKey.DonViLienQuan,
                                             SlotKey.KHTH, SlotKey.HCQT, SlotKey.TCCB, SlotKey.TCKT, SlotKey.CTCD,
                                             SlotKey.PGD1, SlotKey.PGD2, SlotKey.PGD3, SlotKey.GiamDoc })
                {
                    var img = await TryGetSignaturePngAsync(dossier, slot, ct);
                    if (img is null) continue;

                    var alias = slot switch
                    {
                        SlotKey.NguoiTrinh => "SIG_NGUOITRINH_IMAGE",
                        SlotKey.LanhDaoPhong => "SIG_LANHDAO_IMAGE",
                        SlotKey.DonViLienQuan => "SIG_LIENQUAN_IMAGE",
                        SlotKey.KHTH => "KHTH_SIGN_IMAGE",
                        SlotKey.HCQT => "HCQT_SIGN_IMAGE",
                        SlotKey.TCCB => "TCCB_SIGN_IMAGE",
                        SlotKey.TCKT => "TCKT_SIGN_IMAGE",
                        SlotKey.CTCD => "CTCD_SIGN_IMAGE",
                        SlotKey.PGD1 => "PGD1_SIGN_IMAGE",
                        SlotKey.PGD2 => "PGD2_SIGN_IMAGE",
                        SlotKey.PGD3 => "PGD3_SIGN_IMAGE",
                        SlotKey.GiamDoc => "GD_SIGN_IMAGE",
                        _ => null
                    };

                    if (alias != null)
                        OpenXmlTemplateEngine.SetImageByAlias(doc, alias, img);
                }

                // Ẩn các BLOCK_* trống
                foreach (var b in blocksToHide.Distinct(StringComparer.OrdinalIgnoreCase))
                    OpenXmlTemplateEngine.RemoveBlockByAlias(doc, b);
            } // đóng using (doc)

            // ==== Convert → PDF (soffice) ====
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

        // ====================== Helpers ======================
        private static SignTask? FindTask(Dossier dossier, SlotKey key)
            => dossier.SignTasks.FirstOrDefault(t => t.SlotKey == key);

        private static string? GetAssigneeName(Dossier dossier, SlotKey key)
            => FindTask(dossier, key)?.Assignee?.FullName;

        private static string? GetAssigneeDept(Dossier dossier, SlotKey key)
            => FindTask(dossier, key)?.Assignee?.Department?.Name;

        private static string? GetComment(Dossier dossier, SlotKey key)
            => FindTask(dossier, key)?.Comment;

        private async Task<byte[]?> TryGetSignaturePngAsync(Dossier dossier, SlotKey key, CancellationToken ct)
        {
            var task = FindTask(dossier, key);
            if (task?.AssigneeId == null) return null;

            var sig = await _db.UserSignatures
                .Where(s => s.UserId == task.AssigneeId)
                .OrderByDescending(s => s.UploadedAt)
                .FirstOrDefaultAsync(ct);

            return sig?.Data; // byte[] PNG
        }
    }
}
