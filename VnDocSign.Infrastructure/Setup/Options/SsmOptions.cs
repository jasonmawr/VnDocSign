namespace VnDocSign.Infrastructure.Setup.Options
{
    /// <summary>Cấu hình kết nối dịch vụ ký số SSM.</summary>
    public sealed class SsmOptions
    {
        /// <summary>Base URL của SSM, ví dụ "https://10.10.10.5".</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Dùng Bearer token hay không.</summary>
        public bool UseBearer { get; set; } = true;

        /// <summary>Token tĩnh (DEV/INTERNAL) nếu không phát hành theo user.</summary>
        public string? StaticBearer { get; set; }

        /// <summary>Timeout cho HttpClient (giây).</summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>Relative endpoint để ký PDF.</summary>
        public string EndpointPath { get; set; } = "/api/sign/pdf";
    }
}
