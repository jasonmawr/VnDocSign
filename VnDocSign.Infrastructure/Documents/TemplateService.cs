using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnDocSign.Application.Contracts.Dtos.Templates;
using VnDocSign.Application.Contracts.Interfaces.Documents;
using VnDocSign.Application.Contracts.Interfaces.Integration;
using VnDocSign.Domain.Entities.Documents;
using VnDocSign.Infrastructure.Persistence;
using VnDocSign.Infrastructure.Setup.Options;

namespace VnDocSign.Infrastructure.Documents
{
    public sealed class TemplateService : ITemplateService
    {
        private readonly AppDbContext _db;
        private readonly IFileVersioningService _ver;
        private readonly IPdfConverter _pdf;
        private readonly ILogger<TemplateService> _log;
        private readonly string _root;

        public TemplateService(
            AppDbContext db, IFileVersioningService ver, IPdfConverter pdf,
            IOptions<FileStorageOptions> fsOptions, ILogger<TemplateService> log)
        {
            _db = db; _ver = ver; _pdf = pdf; _log = log;
            _root = fsOptions?.Value?.Root ?? "./data";
        }

        public async Task<IReadOnlyList<TemplateListItemDto>> GetAllAsync(CancellationToken ct = default)
        {
            var q = _db.Templates
                .Include(t => t.Versions.OrderByDescending(v => v.VersionNo))
                .AsNoTracking();

            var list = await q.Select(t => new TemplateListItemDto(
                t.Id, t.Code, t.Name, t.IsActive,
                t.Versions.Select(v => (int?)v.Id).FirstOrDefault(),
                t.Versions.Select(v => (int?)v.VersionNo).FirstOrDefault(),
                t.Versions.Select(v => v.FileNameDocx).FirstOrDefault(),
                t.Versions.Select(v => v.FileNamePdf).FirstOrDefault(),
                t.Versions.Select(v => (DateTime?)v.CreatedAt).FirstOrDefault()
            )).ToListAsync(ct);

            return list;
        }

        public async Task<TemplateDetailDto> GetDetailAsync(int id, CancellationToken ct = default)
        {
            var t = await _db.Templates
                .Include(t => t.Versions.OrderByDescending(v => v.VersionNo))
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new InvalidOperationException("Template not found");

            var versions = t.Versions
                .OrderByDescending(v => v.VersionNo)
                .Select(v => new TemplateVersionDto(
                    v.Id, v.VersionNo, v.FileNameDocx, v.FileNamePdf,
                    JsonSerializer.Deserialize<List<string>>(v.VisiblePatternsJson) ?? new List<string>(),
                    v.Notes, v.CreatedAt))
                .ToList();

            return new TemplateDetailDto(t.Id, t.Code, t.Name, t.IsActive, versions);
        }

        public async Task<TemplateUploadResultDto> UploadAsync(
            TemplateUploadRequest meta, IFormFile file, Guid? userId, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("File .docx is required");

            if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only .docx is supported");

            // 1) Tìm hoặc tạo Template
            var t = await _db.Templates.FirstOrDefaultAsync(x => x.Code == meta.TemplateCode, ct);
            if (t == null)
            {
                t = new Template
                {
                    Code = meta.TemplateCode,
                    Name = string.IsNullOrWhiteSpace(meta.Name) ? meta.TemplateCode : meta.Name,
                    IsActive = true,
                    CreatedById = userId
                };
                _db.Templates.Add(t);
                await _db.SaveChangesAsync(ct);
            }

            // 2) Tăng VersionNo
            var nextVer = await _db.TemplateVersions
                .Where(v => v.TemplateId == t.Id)
                .Select(v => (int?)v.VersionNo).MaxAsync(ct) ?? 0;
            nextVer += 1;

            // 3) Lưu file vật lý theo scheme đã dùng trong FileVersioningService
            var folder = Path.Combine(_root, "templates", t.Id.ToString(), $"v{nextVer}");
            Directory.CreateDirectory(folder);
            var docxPath = Path.Combine(folder, "Source.docx");
            using (var fs = File.Create(docxPath)) { await file.CopyToAsync(fs, ct); }

            // 4) Convert PDF
            var pdfPath = await _pdf.ConvertDocxToPdfAsync(docxPath, ct);

            // 5) Trích VisiblePatterns (##{S1}## …) + (tùy chọn) alias Content Controls
            var visible = ExtractVisiblePatterns(docxPath);

            // 6) Tạo TemplateVersion
            var ver = new TemplateVersion
            {
                TemplateId = t.Id,
                VersionNo = nextVer,
                FileNameDocx = Path.GetRelativePath(_root, docxPath).Replace('\\', '/'),
                FileNamePdf = Path.GetRelativePath(_root, pdfPath).Replace('\\', '/'),
                VisiblePatternsJson = JsonSerializer.Serialize(visible),
                Notes = meta.Notes,
                CreatedById = userId
            };
            _db.TemplateVersions.Add(ver);
            await _db.SaveChangesAsync(ct);

            return new TemplateUploadResultDto(t.Id, ver.Id, ver.VersionNo, ver.FileNameDocx, ver.FileNamePdf, visible);
        }

        public async Task<TemplateListItemDto> ToggleActiveAsync(int id, CancellationToken ct = default)
        {
            var t = await _db.Templates
                .Include(x => x.Versions.OrderByDescending(v => v.VersionNo))
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException("Template not found");

            t.IsActive = !t.IsActive;
            await _db.SaveChangesAsync(ct);

            var latest = t.Versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();

            return new TemplateListItemDto(
                t.Id,
                t.Code,
                t.Name,
                t.IsActive,
                latest?.Id,
                latest?.VersionNo,
                latest?.FileNameDocx,
                latest?.FileNamePdf,
                latest?.CreatedAt
            );
        }

        public async Task DeleteVersionAsync(int versionId, CancellationToken ct = default)
        {
            var v = await _db.TemplateVersions.FirstOrDefaultAsync(x => x.Id == versionId, ct)
                ?? throw new InvalidOperationException("Version not found");

            _db.TemplateVersions.Remove(v); // hard delete file record; file vật lý vẫn giữ (hoặc bạn tự cleanup)
            await _db.SaveChangesAsync(ct);
        }

        private static List<string> ExtractVisiblePatterns(string docxPath)
        {
            // Đọc toàn văn bản để tìm marker ký số dạng ##{S1}## ... ##{S13}##
            var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var doc = WordprocessingDocument.Open(docxPath, false);
            var bodyText = doc.MainDocumentPart?.Document?.InnerText ?? string.Empty;

            foreach (Match m in Regex.Matches(bodyText, @"##\{S(\d{1,2})\}##"))
                list.Add(m.Value);

            return list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
