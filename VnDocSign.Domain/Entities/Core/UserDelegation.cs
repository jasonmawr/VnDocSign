using VnDocSign.Domain.Entities.Core;

namespace VnDocSign.Domain.Entities.Core
{
    /// <summary>
    /// Ủy quyền ký: FromUser ủy quyền cho ToUser ký thay trong một khoảng thời gian.
    /// Áp dụng cho mọi SignTask của FromUser (phiên bản v1, chưa phân theo SlotKey).
    /// </summary>
    public sealed class UserDelegation
    {
        public Guid Id { get; set; }

        /// <summary>Người ủy quyền (chủ slot ký gốc).</summary>
        public Guid FromUserId { get; set; }

        /// <summary>Người được ủy quyền (ký thay).</summary>
        public Guid ToUserId { get; set; }

        /// <summary>Thời điểm bắt đầu hiệu lực (UTC).</summary>
        public DateTime StartUtc { get; set; }

        /// <summary>Thời điểm kết thúc hiệu lực (UTC). Nếu null thì vô thời hạn.</summary>
        public DateTime? EndUtc { get; set; }

        /// <summary>Cờ bật/tắt ủy quyền.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Thời điểm tạo (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public User? FromUser { get; set; }
        public User? ToUser { get; set; }
    }
}
