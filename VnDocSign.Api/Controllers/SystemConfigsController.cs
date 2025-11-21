using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Configs;
using VnDocSign.Application.Contracts.Interfaces.Configs;

namespace VnDocSign.Api.Controllers;

[Authorize(Policy = "RequireAdmin")]
[ApiController]
[Route("api/system-configs")]
public sealed class SystemConfigsController : ControllerBase
{
    private readonly ISystemConfigService _svc;

    public SystemConfigsController(ISystemConfigService svc)
    {
        _svc = svc;
    }

    /// <summary>
    /// Lấy toàn bộ cấu hình các slot ký (KHTH, HCQT, TCCB, PGD1, GiamDoc...).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SystemConfigItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var data = await _svc.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<SystemConfigItemDto>>.SuccessResponse(data));
    }

    /// <summary>
    /// Cập nhật cấu hình 1 slot theo Id.
    /// - Slot phòng chức năng (KHTH, HCQT, TCCB, TCKT, CTCD): bắt buộc DepartmentId.
    /// - Slot lãnh đạo (PGD1-3, GiamDoc): bắt buộc UserId.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SystemConfigUpdateRequest req,
        CancellationToken ct)
    {
        var dto = await _svc.UpdateAsync(id, req, ct);
        return Ok(ApiResponse<SystemConfigItemDto>.SuccessResponse(dto, "Cập nhật cấu hình slot thành công."));
    }
}
