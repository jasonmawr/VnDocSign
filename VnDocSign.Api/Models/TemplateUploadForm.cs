using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace VnDocSign.Api.Models
{
    public sealed class TemplateUploadForm
    {
        // Bắt buộc: mã template (ví dụ: PHIEU_TRINH_V1)
        [Required]
        public string TemplateCode { get; set; } = default!;

        // Tuỳ chọn: tên hiển thị
        public string? Name { get; set; }

        // Tuỳ chọn: ghi chú
        public string? Notes { get; set; }

        // Bắt buộc: file DOCX (mẫu gốc)
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}
