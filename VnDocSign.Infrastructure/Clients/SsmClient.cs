using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VnDocSign.Application.Contracts.Interfaces.Integration;
using VnDocSign.Infrastructure.Setup.Options;
using System.Globalization;

namespace VnDocSign.Infrastructure.Clients
{
    /// <summary>
    /// SSM client gọi dịch vụ ký PDF thật.
    /// Ưu tiên SearchPattern; toạ độ chỉ là fallback.
    /// </summary>
    public sealed class SsmClient : ISsmClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<SsmClient> _logger;
        private readonly SsmOptions _opt;

        public SsmClient(
            IHttpClientFactory httpFactory,
            ILogger<SsmClient> logger,
            IOptions<SsmOptions> opt)
        {
            _httpFactory = httpFactory;
            _logger = logger;
            _opt = opt.Value;
        }

        public async Task<SignPdfResult> SignPdfAsync(SignPdfRequest req, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_opt.BaseUrl))
                    return new SignPdfResult(false, "Ssm:BaseUrl missing", req.OutputPdfPath);

                var http = _httpFactory.CreateClient("ssm");
                http.BaseAddress = new Uri(_opt.BaseUrl!);

                // ===== Bearer header (nếu bật UseBearer) =====
                if (_opt.UseBearer)
                {
                    var token = req.BearerToken ?? _opt.StaticBearer;
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        http.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", token);
                    }
                    else
                    {
                        http.DefaultRequestHeaders.Authorization = null;
                    }
                }
                else
                {
                    http.DefaultRequestHeaders.Authorization = null;
                }

                using var form = new MultipartFormDataContent();

                // ---- meta người ký ----
                form.Add(new StringContent(req.EmpCode ?? string.Empty), "empCode");
                form.Add(new StringContent(req.CertName ?? string.Empty), "certName");
                form.Add(new StringContent(req.Company ?? string.Empty), "company");
                form.Add(new StringContent(req.Title ?? string.Empty), "title");
                form.Add(new StringContent(req.Name ?? string.Empty), "name");

                // ---- loại ký/vị trí ----
                form.Add(new StringContent(req.SignType.ToString()), "signType");                   // thường là "1"
                form.Add(new StringContent(req.SignLocationType.ToString()), "signLocationType");  // 2 = SearchPattern, 1 = Coordinates

                // SearchPattern trước, toạ độ sau
                if (!string.IsNullOrWhiteSpace(req.SearchPattern))
                    form.Add(new StringContent(req.SearchPattern), "searchPattern");

                if (req.Page.HasValue)
                    form.Add(new StringContent(req.Page.Value.ToString(CultureInfo.InvariantCulture)), "page");

                if (req.PositionX.HasValue)
                    form.Add(new StringContent(req.PositionX.Value.ToString("0.##", CultureInfo.InvariantCulture)), "x");

                if (req.PositionY.HasValue)
                    form.Add(new StringContent(req.PositionY.Value.ToString("0.##", CultureInfo.InvariantCulture)), "y");

                // ---- PIN (tuyệt đối không log) ----
                form.Add(new StringContent(req.Pin ?? string.Empty), "pin");

                await using var file = File.OpenRead(req.InputPdfPath);
                form.Add(new StreamContent(file), "file", Path.GetFileName(req.InputPdfPath));

                // Endpoint ký PDF thật của SSM
                var path = string.IsNullOrWhiteSpace(_opt.EndpointPath) ? "/api/sign/pdf" : _opt.EndpointPath;
                var resp = await http.PostAsync(path, form, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("SSM sign failed {Code}: {Msg}", (int)resp.StatusCode, err);
                    return new SignPdfResult(false, $"SSM error {(int)resp.StatusCode}", req.OutputPdfPath);
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var outDir = Path.GetDirectoryName(req.OutputPdfPath)!;
                Directory.CreateDirectory(outDir);
                await File.WriteAllBytesAsync(req.OutputPdfPath, bytes, ct);

                _logger.LogInformation("SSM sign OK pattern={Pattern} page={Page}", req.SearchPattern, req.Page);
                return new SignPdfResult(true, null, req.OutputPdfPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSM sign exception");
                return new SignPdfResult(false, ex.Message, req.OutputPdfPath);
            }
        }
    }
}
