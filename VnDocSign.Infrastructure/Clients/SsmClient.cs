using System.IO;
using System.Net.Http;            // <—
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VnDocSign.Application.Contracts.Interfaces.Integration;

namespace VnDocSign.Infrastructure.Clients
{
    /// <summary>
    /// Stub SSM client: chưa gọi hệ thống SSM thật, chỉ copy InputPdf -> OutputPdf.
    /// </summary>
    public sealed class SsmClient : ISsmClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SsmClient> _logger;
        private readonly string _baseUrl;

        public SsmClient(IHttpClientFactory httpClientFactory, ILogger<SsmClient> logger, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = config["Ssm:BaseUrl"] ?? "http://localhost:5001";
        }

        public async Task<SignPdfResult> SignPdfAsync(SignPdfRequest req, CancellationToken ct = default)
        {
            try
            {
                var outDir = Path.GetDirectoryName(req.OutputPdfPath);
                if (!string.IsNullOrWhiteSpace(outDir))
                    Directory.CreateDirectory(outDir);

                await using var src = File.OpenRead(req.InputPdfPath);
                await using var dst = File.Create(req.OutputPdfPath);
                await src.CopyToAsync(dst, ct);

                _logger.LogInformation(
                    "[SSM-STUB] Copied {In} -> {Out} (pattern={Pattern}, page={Page}, type={Type}/{LocType})",
                    req.InputPdfPath, req.OutputPdfPath, req.SearchPattern, req.Page, req.SignType, req.SignLocationType
                );

                return new SignPdfResult(true, null, req.OutputPdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SSM-STUB] Copy failed");
                return new SignPdfResult(false, ex.Message, req.OutputPdfPath);
            }
        }
    }
}
