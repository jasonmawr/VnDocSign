using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Departments;
using VnDocSign.Application.Contracts.Interfaces.Departments;

namespace VnDocSign.Api.Controllers;

[Authorize(Policy = "RequireAdmin")]
[ApiController]
[Route("api/departments")]
public sealed class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _svc;

    public DepartmentsController(IDepartmentService svc)
    {
        _svc = svc;
    }

    /// <summary>
    /// Lấy danh sách tất cả phòng ban.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<DepartmentResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var data = await _svc.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyCollection<DepartmentResponse>>.SuccessResponse(data));
    }

    /// <summary>
    /// Tạo phòng ban mới.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] DepartmentCreateRequest req,
        CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(req, ct);
        return Ok(ApiResponse<DepartmentResponse>.SuccessResponse(dto, "Tạo phòng ban thành công."));
    }

    /// <summary>
    /// Cập nhật thông tin phòng ban (chỉ cập nhật Name).
    /// Code không cho phép thay đổi sau khi tạo.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] DepartmentCreateRequest req,
        CancellationToken ct)
    {
        var dto = await _svc.UpdateAsync(id, req, ct);
        return Ok(ApiResponse<DepartmentResponse>.SuccessResponse(dto, "Cập nhật phòng ban thành công."));
    }

    /// <summary>
    /// Bật/tắt trạng thái hoạt động của phòng ban.
    /// - Khi vô hiệu hóa: không được phép nếu còn user đang active trong phòng đó.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-active")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleActive(
        Guid id,
        CancellationToken ct)
    {
        var dto = await _svc.ToggleActiveAsync(id, ct);
        var msg = dto.IsActive
            ? "Đã kích hoạt lại phòng ban."
            : "Đã vô hiệu hóa phòng ban.";
        return Ok(ApiResponse<DepartmentResponse>.SuccessResponse(dto, msg));
    }
}
