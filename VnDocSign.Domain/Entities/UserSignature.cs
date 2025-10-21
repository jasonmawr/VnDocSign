using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Core;

namespace VnDocSign.Domain.Entities
{
    public sealed class UserSignature
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string FileName { get; set; } = default!;   // ví dụ: han.png
        public string ContentType { get; set; } = "image/png";
        public byte[] Data { get; set; } = default!;       // ảnh PNG đã tách nền
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
