using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VnDocSign.Application.Contracts.Interfaces.Integration;

namespace VnDocSign.Infrastructure.Documents
{
    /// <summary>
    /// Stub PDF: tạo PDF tối thiểu nếu chưa có; đảm bảo PDF cho file bất kỳ bằng cách wrap content vào PDF dummy.
    /// Không dùng thư viện ngoài để giữ build đơn giản ở GĐ3.
    /// </summary>
    public sealed class PdfRenderService : IPdfRenderService
    {
        private readonly ILogger<PdfRenderService> _logger;
        private readonly IConfiguration _config;

        public PdfRenderService(ILogger<PdfRenderService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        private string GetRoot() => _config["FileStorage:Root"] ?? "./data";

        public async Task<string> RenderDossierToPdf(Guid dossierId, CancellationToken ct = default)
        {
            var root = GetRoot();
            var folder = Path.Combine(root, "dossiers", dossierId.ToString("N"));
            Directory.CreateDirectory(folder);

            var outPath = Path.Combine(folder, "Source.pdf");
            if (!File.Exists(outPath))
            {
                await WriteMinimalPdfAsync(outPath, $"Dossier {dossierId}", ct);
                _logger.LogInformation("[PDF-STUB] Rendered source PDF for dossier {Dossier} at {Path}", dossierId, outPath);
            }
            else
            {
                _logger.LogDebug("[PDF-STUB] Source PDF already exists at {Path}", outPath);
            }

            return outPath;
        }

        public async Task<string> EnsurePdf(string anyPath, CancellationToken ct = default)
        {
            if (anyPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return anyPath;
            }

            var root = GetRoot();
            var folder = Path.Combine(root, "converted");
            Directory.CreateDirectory(folder);
            var filename = Path.GetFileNameWithoutExtension(anyPath) + ".pdf";
            var outPath = Path.Combine(folder, filename);

            await WriteMinimalPdfAsync(outPath, $"Converted from: {anyPath}", ct);
            _logger.LogInformation("[PDF-STUB] Ensured PDF from {Input} -> {Output}", anyPath, outPath);
            return outPath;
        }

        private static async Task WriteMinimalPdfAsync(string path, string title, CancellationToken ct)
        {
            // PDF rất tối thiểu (một trang, text Helvetica). Tránh ký tự () trong text.
            var safeTitle = title.Replace("(", "\\(").Replace(")", "\\)");
            var pdf = $"%PDF-1.4\n" +
                      $"1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                      $"2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n" +
                      $"3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj\n" +
                      $"4 0 obj<</Length 44>>stream\n" +
                      $"BT /F1 24 Tf 72 770 Td ({safeTitle}) Tj ET\n" +
                      $"endstream endobj\n" +
                      $"5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\n" +
                      $"xref\n0 6\n0000000000 65535 f \n0000000010 00000 n \n0000000060 00000 n \n0000000114 00000 n \n0000000264 00000 n \n0000000380 00000 n \n" +
                      $"trailer<</Size 6/Root 1 0 R>>\nstartxref\n460\n%%EOF";
            var bytes = Encoding.ASCII.GetBytes(pdf);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes, ct);
        }
    }
}
