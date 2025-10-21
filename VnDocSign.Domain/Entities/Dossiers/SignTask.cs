using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Domain.Entities.Signing;

namespace VnDocSign.Domain.Entities.Dossiers
{
    public enum SignTaskStatus { Pending, Approved, Rejected }

    public sealed class SignTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DossierId { get; set; }
        public Dossier? Dossier { get; set; }

        public Guid AssigneeId { get; set; }
        public User? Assignee { get; set; }

        public int Order { get; set; }
        public SignTaskStatus Status { get; set; } = SignTaskStatus.Pending;
        public DateTime? DecidedAt { get; set; }
        public string? Comment { get; set; }
        public SlotKey SlotKey { get; set; }          // ô nào trong phiếu
        public SlotPhase Phase { get; set; }          // vùng 1/2/3/Clerk/Director
        public bool IsActivated { get; set; }         // đã mở để ký chưa (theo rule)
        public bool ClerkConfirmed { get; set; }      // chỉ dùng cho VanThuCheck

        public Guid? SignatureImageId { get; set; }   // Id bảng UserSignature (dán vào phiếu)
        public Guid? SignedPdfAttachmentId { get; set; } // để dành khi ký số file đính kèm
        public string? VisiblePattern { get; set; }
    }
}
