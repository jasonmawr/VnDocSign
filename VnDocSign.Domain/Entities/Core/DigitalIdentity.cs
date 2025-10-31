using System;
using VnDocSign.Domain.Entities.Core;

namespace VnDocSign.Domain.Entities.Core
{
    public sealed class DigitalIdentity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string EmpCode { get; set; } = default!;       // <=100
        public string CertName { get; set; } = default!;      // <=256
        public string Company { get; set; } = default!;       // <=128

        public string? DisplayName { get; set; }              // <=128 (optional)
        public string? Title { get; set; }                    // <=128 (optional)

        public bool IsActive { get; set; } = true;            // mỗi user chỉ 1 Active
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // ===== NEW (GĐ5.1) =====
        public string? Provider { get; set; }                 // <=64  (VIS/EASY/ASOFT...)
        public DateTime? NotBefore { get; set; }              // hiệu lực từ
        public DateTime? NotAfter { get; set; }              // hiệu lực đến
        public string? SerialNo { get; set; }                 // <=128
        public string? Issuer { get; set; }                   // <=256
        public string? Subject { get; set; }                  // <=256
    }
}
