using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    /// <summary>
    /// Service chịu trách nhiệm:
    /// - Render hồ sơ (Dossier) thành Source.docx + Source.pdf (phiếu trình chưa ký)
    /// - Render phiên bản đã ký MOCK: Signed_vN.docx + Signed_vN.pdf (chèn PNG chữ ký)
    /// - Đảm bảo convert DOCX -> PDF
    /// </summary>
    public sealed class PdfRenderService : IPdfRenderService
    {
        private readonly AppDbContext _db;
        private readonly IPdfConverter _pdfConverter;
        private readonly ILogger<PdfRenderService> _logger;
        private readonly string _root;

        // ========================= Nested types =========================

        /// <summary>
        /// Ngữ cảnh render cho 1 Dossier: dữ liệu hồ sơ, nội dung, template, thư mục vật lý.
        /// </summary>
        private sealed record DossierRenderContext(
            Dossier Dossier,
            DossierContent Content,
            string DossierDir,
            string TemplateDocxPath,
            string SourceDocxPath,
            string SourcePdfPath
        );

        // ========================= Ctor =========================

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

        // ========================= Public API =========================

        /// <summary>
        /// Render phiếu trình (chưa ký) ra PDF (Source.pdf).
        /// - Từ Template + DossierContent.DataJson + SignTasks
        /// - Ẩn/hiện các BLOCK_* phù hợp
        /// </summary>
        public async Task<string> RenderDossierToPdf(Guid dossierId, CancellationToken ct = default)
        {
            var ctx = await LoadContextAsync(dossierId, ct).ConfigureAwait(false);
            await EnsureSourceDocxAndPdfAsync(ctx, ct).ConfigureAwait(false);
            return Path.GetFullPath(ctx.SourcePdfPath);
        }

        /// <summary>
        /// Đảm bảo một file bất kỳ là PDF:
        /// - Nếu đã là .pdf: trả về đường dẫn tuyệt đối
        /// - Nếu là .docx: convert sang PDF
        /// - Ngược lại: NotSupportedException
        /// </summary>
        public Task<string> EnsurePdf(string anyPath, CancellationToken ct = default)
        {
            if (anyPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Path.GetFullPath(anyPath));

            if (anyPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return _pdfConverter.ConvertDocxToPdfAsync(anyPath, ct);

            throw new NotSupportedException("Only DOCX→PDF is supported in this stage.");
        }

        /// <summary>
        /// Tạo bản Signed_vN.docx/pdf cho chế độ ký MOCK.
        /// Quy trình:
        /// - Đảm bảo đã render xong Source.docx/Source.pdf
        /// - Copy Source.docx -> Signed_vN.docx (N = max version + 1)
        /// - Chèn ảnh chữ ký PNG vào alias SIG_* tương ứng các SignTask đã Approved
        /// - Convert -> Signed_vN.pdf và trả về đường dẫn tuyệt đối
        /// </summary>
        public async Task<string> RenderSignedMockAsync(Guid dossierId, CancellationToken ct = default)
        {
            var ctx = await LoadContextAsync(dossierId, ct).ConfigureAwait(false);

            // 1) Đảm bảo Source.docx + Source.pdf đã tồn tại / được render
            await EnsureSourceDocxAndPdfAsync(ctx, ct).ConfigureAwait(false);

            // 2) Tính version mới cho Signed_vN
            var version = GetNextSignedVersion(ctx.DossierDir);
            var signedDocx = Path.Combine(ctx.DossierDir, $"Signed_v{version}.docx");
            var signedPdf = Path.Combine(ctx.DossierDir, $"Signed_v{version}.pdf");

            // 3) Copy Source.docx -> Signed_vN.docx
            File.Copy(ctx.SourceDocxPath, signedDocx, overwrite: true);

            // 4) Chèn ảnh chữ ký theo các SignTask đã Approved
            await InsertSignatureImagesAsync(ctx.Dossier, signedDocx, ct).ConfigureAwait(false);

            // 5) Convert DOCX -> PDF
            var outPdf = await _pdfConverter.ConvertDocxToPdfAsync(signedDocx, ct).ConfigureAwait(false);
            if (!File.Exists(outPdf))
                throw new InvalidOperationException("Failed to convert signed PDF.");

            // Đảm bảo tên file đúng Signed_vN.pdf
            MoveOrReplace(outPdf, signedPdf);

            return Path.GetFullPath(signedPdf);
        }

        // ========================= Core: Context & Source render =========================

        /// <summary>
        /// Tải đầy đủ Dossier + DossierContent + Template version mới nhất
        /// và chuẩn bị thư mục + path vật lý.
        /// </summary>
        private async Task<DossierRenderContext> LoadContextAsync(Guid dossierId, CancellationToken ct)
        {
            // 1) Dossier + CreatedBy + Dept + SignTasks + Assignee + Dept
            var dossier = await _db.Dossiers
                .Include(x => x.CreatedBy)
                    .ThenInclude(u => u!.Department)
                .Include(x => x.SignTasks)
                    .ThenInclude(t => t.Assignee)
                        .ThenInclude(u => u!.Department)
                .FirstOrDefaultAsync(x => x.Id == dossierId, ct)
                .ConfigureAwait(false);

            if (dossier == null)
                throw new InvalidOperationException($"Dossier {dossierId} not found.");

            // 2) DossierContent
            var content = await _db.DossierContents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DossierId == dossierId, ct)
                .ConfigureAwait(false);

            if (content == null || string.IsNullOrWhiteSpace(content.TemplateCode))
                throw new InvalidOperationException("Dossier has no TemplateCode. Please set content with a valid TemplateCode before rendering.");

            // 3) Template version mới nhất theo TemplateCode
            var template = await _db.Templates
                .Include(t => t.Versions.OrderByDescending(v => v.VersionNo))
                .FirstOrDefaultAsync(t => t.Code == content.TemplateCode, ct)
                .ConfigureAwait(false);

            if (template == null || template.Versions.Count == 0)
                throw new FileNotFoundException($"No template version found for code: {content.TemplateCode}");

            var latestVer = template.Versions.First(); // do đã OrderByDescending

            var rootFullPath = Path.GetFullPath(_root);
            var templateDocxPath = Path.Combine(rootFullPath, latestVer.FileNameDocx);

            if (!File.Exists(templateDocxPath))
                throw new FileNotFoundException("Template DOCX not found.", templateDocxPath);

            // 4) Thư mục hồ sơ & Source path
            var dossierDir = Path.Combine(rootFullPath, "dossiers", dossierId.ToString("N"));
            Directory.CreateDirectory(dossierDir);

            var sourceDocx = Path.Combine(dossierDir, "Source.docx");
            var sourcePdf = Path.Combine(dossierDir, "Source.pdf");

            return new DossierRenderContext(
                dossier,
                content,
                dossierDir,
                templateDocxPath,
                sourceDocx,
                sourcePdf
            );
        }

        /// <summary>
        /// Đảm bảo Source.docx + Source.pdf tồn tại và đúng nội dung:
        /// - Copy template DOCX về thư mục hồ sơ
        /// - Điền alias text (DataJson + SignTasks)
        /// - Ẩn/hiện BLOCK_* theo slot
        /// - Convert DOCX → PDF
        /// </summary>
        private async Task EnsureSourceDocxAndPdfAsync(DossierRenderContext ctx, CancellationToken ct)
        {
            try
            {
                // Luôn render lại để đảm bảo nội dung mới nhất (nếu bạn muốn cache, có thể thêm điều kiện tại đây)
                File.Copy(ctx.TemplateDocxPath, ctx.SourceDocxPath, overwrite: true);

                // 1) Chuẩn bị map alias text từ DataJson + SignTasks
                var aliasFromDb = ParseAliasFromJson(ctx.Content.DataJson);
                var richTextMap = BuildRichTextMap(ctx.Dossier, aliasFromDb);

                // 2) Xác định BLOCK_* cần ẩn
                var blocksToHide = BuildBlocksToHide(ctx.Dossier);

                // 3) Fill alias + ẩn block trên Source.docx
                using (var doc = WordprocessingDocument.Open(ctx.SourceDocxPath, true))
                {
                    // 3.1) Fill toàn bộ alias text
                    foreach (var kv in richTextMap)
                    {
                        var value = kv.Value ?? string.Empty;
                        OpenXmlTemplateEngine.FillRichTextByAlias(doc, kv.Key, value);
                    }

                    // 3.2) Ẩn các block không dùng
                    foreach (var block in blocksToHide.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        OpenXmlTemplateEngine.RemoveBlockByAlias(doc, block);
                    }
                }

                // 4) Convert DOCX → PDF
                var outPdf = await _pdfConverter.ConvertDocxToPdfAsync(ctx.SourceDocxPath, ct).ConfigureAwait(false);
                if (!File.Exists(outPdf))
                    throw new InvalidOperationException("Failed to convert Source DOCX to PDF.");

                MoveOrReplace(outPdf, ctx.SourcePdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering Source for dossier {DossierId}", ctx.Dossier.Id);
                throw;
            }
        }

        // ========================= Core: Signed_vN (mock) =========================

        /// <summary>
        /// Tính version mới cho Signed_vN dựa trên các file Signed_v*.pdf tồn tại.
        /// </summary>
        private static int GetNextSignedVersion(string dossierDir)
        {
            var existingSigned = Directory.GetFiles(dossierDir, "Signed_v*.pdf");
            var maxVersion = 0;

            foreach (var path in existingSigned)
            {
                var fileName = Path.GetFileNameWithoutExtension(path); // "Signed_v3"
                var idx = fileName.LastIndexOf('v');
                if (idx >= 0 && int.TryParse(fileName[(idx + 1)..], out var n) && n > maxVersion)
                {
                    maxVersion = n;
                }
            }

            return maxVersion + 1;
        }

        /// <summary>
        /// Chèn ảnh chữ ký PNG vào Signed_vN.docx theo các SignTask đã Approved.
        /// </summary>
        private async Task InsertSignatureImagesAsync(Dossier dossier, string signedDocxPath, CancellationToken ct)
        {
            // Map SlotKey -> alias ảnh trong DOCX (theo tài liệu phiếu trình)
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

            // Chỉ chèn ảnh cho các slot có SignTask Approved
            var pngByAlias = new Dictionary<string, byte[]>();
            foreach (var kv in imageAliasBySlot)
            {
                var slotKey = kv.Key;
                var alias = kv.Value;

                var task = FindTask(dossier, slotKey);
                if (task == null || task.Status != SignTaskStatus.Approved)
                    continue;

                var png = await TryGetSignaturePngAsync(task.AssigneeId, ct).ConfigureAwait(false);
                if (png != null && png.Length > 0)
                {
                    pngByAlias[alias] = png;
                }
            }

            if (pngByAlias.Count == 0)
                return;

            using var doc = WordprocessingDocument.Open(signedDocxPath, true);
            foreach (var kv in pngByAlias)
            {
                OpenXmlTemplateEngine.SetImageByAlias(doc, kv.Key, kv.Value);
            }
        }

        // ========================= Helpers: alias & block =========================

        /// <summary>
        /// Parse DataJson thành dictionary alias -> value. Nếu json lỗi, trả về dictionary rỗng.
        /// </summary>
        private static Dictionary<string, string?> ParseAliasFromJson(string? dataJson)
        {
            if (string.IsNullOrWhiteSpace(dataJson))
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(dataJson);
                return dict ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // JSON lỗi thì bỏ qua, không làm hỏng cả quá trình render
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Xây dựng map alias -> text cho phiếu trình
        /// (ưu tiên alias từ DataJson, còn lại build từ Dossier + SignTasks).
        /// </summary>
        private static Dictionary<string, string?> BuildRichTextMap(
            Dossier dossier,
            IDictionary<string, string?> aliasFromDb)
        {
            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                // Thông tin chung
                ["VU_VIEC"] = dossier.Title ?? string.Empty,
                ["DON_VI"] = dossier.CreatedBy?.Department?.Name ?? string.Empty,
                ["KINH_GUI"] = "Ban Giám đốc", // có thể cấu hình thêm
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

            // Ghi đè từ DataJson (ưu tiên theo nội dung người dùng nhập)
            foreach (var kv in aliasFromDb)
            {
                map[kv.Key] = kv.Value ?? string.Empty;
            }

            return map;
        }

        /// <summary>
        /// Xác định danh sách BLOCK_* cần ẩn dựa trên việc có/không có người phụ trách slot.
        /// </summary>
        private static List<string> BuildBlocksToHide(Dossier dossier)
        {
            var blocks = new List<string>();

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.DonViLienQuan)))
                blocks.Add("BLOCK_LIENQUAN");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.KHTH)))
                blocks.Add("BLOCK_KHTH");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.HCQT)))
                blocks.Add("BLOCK_HCQT");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.TCCB)))
                blocks.Add("BLOCK_TCCB");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.TCKT)))
                blocks.Add("BLOCK_TCKT");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.CTCD)))
                blocks.Add("BLOCK_CTCD");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.PGD1)))
                blocks.Add("BLOCK_PGD_1");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.PGD2)))
                blocks.Add("BLOCK_PGD_2");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.PGD3)))
                blocks.Add("BLOCK_PGD_3");

            if (string.IsNullOrWhiteSpace(GetAssigneeName(dossier, SlotKey.GiamDoc)))
                blocks.Add("BLOCK_GD");

            return blocks;
        }

        // ========================= Helpers: Dossier & Signature =========================

        private static SignTask? FindTask(Dossier dossier, SlotKey key)
            => dossier.SignTasks?.FirstOrDefault(t => t.SlotKey == key);

        private static string? GetAssigneeName(Dossier dossier, SlotKey key)
        {
            var task = FindTask(dossier, key);
            var name = task?.Assignee?.FullName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private static string? GetAssigneeDept(Dossier dossier, SlotKey key)
            => FindTask(dossier, key)?.Assignee?.Department?.Name;

        private static string? GetComment(Dossier dossier, SlotKey key)
            => FindTask(dossier, key)?.Comment;

        /// <summary>
        /// Lấy ảnh PNG chữ ký mới nhất của user (nếu có).
        /// </summary>
        private async Task<byte[]?> TryGetSignaturePngAsync(Guid assigneeId, CancellationToken ct)
        {
            if (assigneeId == Guid.Empty)
                return null;

            var sig = await _db.UserSignatures
                .Where(s => s.UserId == assigneeId)
                .OrderByDescending(s => s.UploadedAt)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            return sig?.Data;
        }

        /// <summary>
        /// Di chuyển file nguồn sang đích, nếu đích đã tồn tại thì xóa và thay thế.
        /// </summary>
        private static void MoveOrReplace(string sourcePath, string destPath)
        {
            var srcFull = Path.GetFullPath(sourcePath);
            var dstFull = Path.GetFullPath(destPath);

            if (string.Equals(srcFull, dstFull, StringComparison.OrdinalIgnoreCase))
                return;

            if (File.Exists(dstFull))
                File.Delete(dstFull);

            File.Move(srcFull, dstFull);
        }
    }
}
