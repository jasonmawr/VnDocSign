using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Dossiers;

namespace VnDocSign.Application.Contracts.Interfaces.Dossiers;

public interface IDossierService
{
    /// <summary>
    /// Tạo mới Dossier + các SignTask Vùng 1 (Người trình, LĐ phòng).
    /// </summary>
    Task<DossierCreateResponse> CreateAsync(DossierCreateRequest req, CancellationToken ct = default);

    /// <summary>
    /// Cập nhật route: phòng liên quan + các phòng chức năng.
    /// Từ đó sinh các SignTask Vùng 1 (Đơn vị liên quan), Vùng 2, Vùng 3, Văn thư, Giám đốc.
    /// </summary>
    Task RouteAsync(
        Guid dossierId,
        Guid? relatedDepartmentId,
        IReadOnlyCollection<string> selectedFunctionalSlots,
        CancellationToken ct = default);

    /// <summary>
    /// Đảm bảo user hiện tại có quyền render hồ sơ:
    /// - Là người tạo hồ sơ
    /// - Hoặc là người nằm trong tuyến ký (SignTask.Assignee)
    /// Ném UnauthorizedAccessException nếu không đủ quyền.
    /// </summary>
    Task EnsureCanRenderAsync(
        Guid dossierId,
        Guid userId,
        CancellationToken ct = default);

    ///<summary>Danh sách hồ sơ do user hiện tại tạo</summary>
    Task<IReadOnlyList<DossierListItemDto>> GetMyCreatedAsync(
        Guid userId,
        CancellationToken ct = default);
}
