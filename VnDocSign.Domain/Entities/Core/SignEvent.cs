using System;
using VnDocSign.Domain.Entities.Dossiers;

namespace VnDocSign.Domain.Entities.Dossiers
{
    public sealed class SignEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DossierId { get; set; }
        public Dossier? Dossier { get; set; }

        public Guid ActorUserId { get; set; }

        public string PdfPathIn { get; set; } = default!;     // <=1024
        public string PdfPathOut { get; set; } = default!;    // <=1024

        public int SignType { get; set; }                     // theo chuẩn SSM
        public int SignLocationType { get; set; }             // SearchPattern / Toạ độ

        public string? SearchPattern { get; set; }            // <=256
        public int? Page { get; set; }
        public float? PositionX { get; set; }
        public float? PositionY { get; set; }

        public string? VisibleSignatureName { get; set; }     // <=128
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
