namespace VnDocSign.Infrastructure.Setup.Options
{
    /// <summary>Cấu hình kết nối dịch vụ ký số SSM.</summary>
    public sealed class SsmOptions
    {
        /// <summary>Base URL của SSM, ví dụ: "https://ssm.yourdomain".</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Dùng Bearer token hay không.</summary>
        public bool UseBearer { get; set; } = true;

        /// <summary>Token tĩnh (nếu không phát hành theo user).</summary>
        public string? StaticBearer { get; set; }
    }
}
