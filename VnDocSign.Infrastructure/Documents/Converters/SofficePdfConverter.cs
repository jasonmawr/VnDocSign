using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnDocSign.Application.Contracts.Interfaces.Integration;


namespace VnDocSign.Infrastructure.Documents.Converters
{
    public sealed class SofficePdfConverter : IPdfConverter
    {
        private readonly ILogger<SofficePdfConverter> _logger;
        private readonly string _sofficePath;
        private readonly int _timeoutSeconds;


        public sealed class Options
        {
            /// <summary>Đường dẫn thực thi LibreOffice soffice (ví dụ: "soffice" trong PATH hoặc "C:\\Program Files\\LibreOffice\\program\\soffice.exe").</summary>
            public string? SofficePath { get; set; }
            /// <summary>Timeout (giây) cho 1 lần convert.</summary>
            public int TimeoutSeconds { get; set; } = 120;
        }


        public SofficePdfConverter(ILogger<SofficePdfConverter> logger, IOptions<Options> options)
        {
            _logger = logger;
            _sofficePath = string.IsNullOrWhiteSpace(options.Value.SofficePath) ? "soffice" : options.Value.SofficePath!;
            _timeoutSeconds = options.Value.TimeoutSeconds <= 0 ? 120 : options.Value.TimeoutSeconds;
        }


        public async Task<string> ConvertDocxToPdfAsync(string docxPath, CancellationToken ct = default)
        {
            if (!File.Exists(docxPath)) throw new FileNotFoundException("DOCX not found", docxPath);


            var srcDir = Path.GetDirectoryName(Path.GetFullPath(docxPath))!;
            var psi = new ProcessStartInfo
            {
                FileName = _sofficePath,
                Arguments = $"--headless --convert-to pdf --outdir \"{srcDir}\" \"{docxPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };


            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _logger.LogInformation("Converting DOCX to PDF with soffice: {Docx}", docxPath);
            proc.Start();


            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));


            var exitedTcs = new TaskCompletionSource<bool>();
            proc.Exited += (_, __) => exitedTcs.TrySetResult(true);


            var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != exitedTcs.Task)
            {
                try { proc.Kill(true); } catch { /* ignore */ }
                throw new TimeoutException("soffice convert timeout");
            }


            var pdfPath = Path.ChangeExtension(docxPath, ".pdf");
            if (!File.Exists(pdfPath)) throw new InvalidOperationException("PDF not produced by soffice");
            return pdfPath;
        }
    }
}