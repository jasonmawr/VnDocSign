using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VnDocSign.Application.Contracts.Dtos.Configs;

namespace VnDocSign.Application.Contracts.Interfaces.Configs
{
    public interface ISystemConfigService
    {
        /// <summary>
        /// Lấy toàn bộ cấu hình các slot (KHTH, HCQT, PGD1, GiamDoc...).
        /// </summary>
        Task<IReadOnlyList<SystemConfigItemDto>> GetAllAsync(CancellationToken ct = default);

        /// <summary>
        /// Cập nhật 1 dòng SystemConfig theo Id.
        /// </summary>
        Task<SystemConfigItemDto> UpdateAsync(int id, SystemConfigUpdateRequest req, CancellationToken ct = default);
    }
}
