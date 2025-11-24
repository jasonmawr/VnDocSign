using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VnDocSign.Application.Common;
using VnDocSign.Application.Contracts.Dtos.Users;
using VnDocSign.Application.Contracts.Interfaces.Users;

namespace VnDocSign.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/delegations")]
    public sealed class DelegationsController : ControllerBase
    {
        private readonly IUserDelegationService _svc;

        public DelegationsController(IUserDelegationService svc)
        {
            _svc = svc;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("uid") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !Guid.TryParse(claim.Value, out var id))
                throw new UnauthorizedAccessException("Không xác định được user từ token.");
            return id;
        }

        /// <summary>Các ủy quyền mà tôi là người ủy quyền (FromUser).</summary>
        [HttpGet("mine")]
        [ProducesResponseType(typeof(ApiResponse<List<UserDelegationDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken ct)
        {
            var me = GetCurrentUserId();
            var data = await _svc.GetOwnedAsync(me, ct);
            return Ok(ApiResponse.SuccessResponse(data));
        }

        /// <summary>Các ủy quyền mà tôi là người được ủy (ToUser).</summary>
        [HttpGet("for-me")]
        [ProducesResponseType(typeof(ApiResponse<List<UserDelegationDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetForMe(CancellationToken ct)
        {
            var me = GetCurrentUserId();
            var data = await _svc.GetDelegatedToMeAsync(me, ct);
            return Ok(ApiResponse.SuccessResponse(data));
        }

        /// <summary>Tạo ủy quyền mới (FromUser = user hiện tại).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<UserDelegationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create([FromBody] UserDelegationCreateRequest req, CancellationToken ct)
        {
            var me = GetCurrentUserId();
            var dto = await _svc.CreateAsync(me, req, ct);
            return Ok(ApiResponse.SuccessResponse(dto, "Tạo ủy quyền ký thành công."));
        }

        /// <summary>Cập nhật ủy quyền (chỉ chủ ủy quyền được sửa).</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<UserDelegationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UserDelegationUpdateRequest req, CancellationToken ct)
        {
            var me = GetCurrentUserId();
            var dto = await _svc.UpdateAsync(me, id, req, ct);
            return Ok(ApiResponse.SuccessResponse(dto, "Cập nhật ủy quyền ký thành công."));
        }

        /// <summary>Hủy ủy quyền.</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var me = GetCurrentUserId();
            await _svc.DeleteAsync(me, id, ct);
            return Ok(ApiResponse.SuccessResponse("Hủy ủy quyền ký thành công."));
        }
    }
}