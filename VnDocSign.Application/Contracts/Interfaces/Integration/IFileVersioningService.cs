using System;
using System.Threading;
using System.Threading.Tasks;

namespace VnDocSign.Application.Contracts.Interfaces.Integration
{
    /// <summary>
    /// Quản lý version hoá file PDF ký: Signed_vN.pdf + current.pointer.
    /// </summary>
    public interface IFileVersioningService
    {
        /// <summary>
        /// Lưu file PDF đã ký (file tạm) thành bản Signed_vN.pdf trong thư mục hồ sơ,
        /// cập nhật current.pointer và trả về đường dẫn tuyệt đối tới bản mới.
        /// </summary>
        Task<string> SaveSignedVersionAsync(Guid dossierId, string signedTempFile, CancellationToken ct = default);

        /// <summary>
        /// Đọc current.pointer → đường dẫn tuyệt đối file PDF hiện hành (nếu có).
        /// </summary>
        Task<string?> GetCurrentPointerAsync(Guid dossierId, CancellationToken ct = default);

        /// <summary>
        /// Cập nhật current.pointer trỏ tới file PDF tuyệt đối chỉ định.
        /// </summary>
        Task SetCurrentPointerAsync(Guid dossierId, string absolutePdfPath, CancellationToken ct = default);
    }
}
