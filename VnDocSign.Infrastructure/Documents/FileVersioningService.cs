using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VnDocSign.Application.Contracts.Interfaces.Integration;

namespace VnDocSign.Infrastructure.Documents
{
    public sealed class FileVersioningService : IFileVersioningService
    {
        private readonly string _root;

        public FileVersioningService(IConfiguration cfg)
        {
            _root = cfg["FileStorage:Root"] ?? "./data";
        }

        // ===== Helpers =====
        private string GetDossierFolder(Guid dossierId)
        {
            var folder = Path.Combine(_root, "dossiers", dossierId.ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static int VersionOf(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            var m = Regex.Match(name, @"Signed_v(\d+)\.pdf", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : 0;
        }

        private IReadOnlyList<string> ListVersionFiles(string folder)
        {
            if (!Directory.Exists(folder)) return Array.Empty<string>();

            return Directory.EnumerateFiles(folder, "Signed_v*.pdf")
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(VersionOf)
                .ToList();
        }

        private string GetPointerPath(string folder)
            => Path.Combine(folder, "current.pointer");

        // ===== Interface =====
        public async Task<string> SaveSignedVersionAsync(Guid dossierId, string signedTempFile, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(signedTempFile) || !File.Exists(signedTempFile))
                throw new FileNotFoundException("Signed temp file not found.", signedTempFile);

            var folder = GetDossierFolder(dossierId);

            var max = ListVersionFiles(folder).DefaultIfEmpty().Max(VersionOf);
            var next = Math.Max(0, max) + 1;

            var dest = Path.Combine(folder, $"Signed_v{next}.pdf");

            // .NET 8: di chuyển file tạm sang đích, không overwrite
            File.Move(signedTempFile, dest, overwrite: false);

            await SetCurrentPointerAsync(dossierId, dest, ct);
            return dest;
        }

        public async Task<string?> GetCurrentPointerAsync(Guid dossierId, CancellationToken ct = default)
        {
            var folder = GetDossierFolder(dossierId);
            var pointer = GetPointerPath(folder);
            if (!File.Exists(pointer)) return null;

            var raw = (await File.ReadAllTextAsync(pointer, ct)).Trim();
            return string.IsNullOrEmpty(raw) ? null : raw;
        }

        public Task SetCurrentPointerAsync(Guid dossierId, string absolutePdfPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(absolutePdfPath))
                throw new ArgumentException("absolutePdfPath is required.", nameof(absolutePdfPath));

            var folder = GetDossierFolder(dossierId);
            var pointer = GetPointerPath(folder);

            var abs = Path.GetFullPath(absolutePdfPath);
            return File.WriteAllTextAsync(pointer, abs, ct);
        }
    }
}
