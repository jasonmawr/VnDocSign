using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Configs;
using VnDocSign.Application.Contracts.Interfaces.Configs;
using VnDocSign.Domain.Entities.Config;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Domain.Entities.Signing;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services
{
    public sealed class SystemConfigService : ISystemConfigService
    {
        private readonly AppDbContext _db;

        public SystemConfigService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SystemConfigItemDto>> GetAllAsync(CancellationToken ct = default)
        {
            var configs = await _db.SystemConfigs
                .AsNoTracking()
                .ToListAsync(ct);

            var deptIds = configs
                .Where(c => c.DepartmentId.HasValue)
                .Select(c => c.DepartmentId!.Value)
                .Distinct()
                .ToList();

            var userIds = configs
                .Where(c => c.UserId.HasValue)
                .Select(c => c.UserId!.Value)
                .Distinct()
                .ToList();

            var departments = await _db.Departments
                .Where(d => deptIds.Contains(d.Id))
                .AsNoTracking()
                .ToListAsync(ct);

            var users = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .AsNoTracking()
                .ToListAsync(ct);

            var deptIndex = departments.ToDictionary(d => d.Id, d => d);
            var userIndex = users.ToDictionary(u => u.Id, u => u);

            var items = configs
                .OrderBy(c => c.Slot)
                .Select(c =>
                {
                    deptIndex.TryGetValue(c.DepartmentId ?? Guid.Empty, out var dep);
                    userIndex.TryGetValue(c.UserId ?? Guid.Empty, out var user);

                    return ToDto(c, dep, user);
                })
                .ToList();

            return items;
        }

        public async Task<SystemConfigItemDto> UpdateAsync(int id, SystemConfigUpdateRequest req, CancellationToken ct = default)
        {
            var cfg = await _db.SystemConfigs
                .FirstOrDefaultAsync(c => c.Id == id, ct)
                ?? throw new KeyNotFoundException("SystemConfig not found.");

            var slot = cfg.Slot;
            var isFunctional = IsFunctionalDepartmentSlot(slot);
            var isLeaderSlot = IsLeadershipSlot(slot);

            // Slot Vùng 2: KHTH, HCQT, TCCB, TCKT, CTCD -> bắt buộc DepartmentId, không dùng UserId
            if (isFunctional)
            {
                if (!req.DepartmentId.HasValue)
                    throw new InvalidOperationException("DepartmentId is required for functional department slot.");

                cfg.DepartmentId = req.DepartmentId;
                cfg.UserId = null;
            }
            // Slot lãnh đạo: PGD1-3, GiamDoc -> bắt buộc UserId, không dùng DepartmentId
            else if (isLeaderSlot)
            {
                if (!req.UserId.HasValue)
                    throw new InvalidOperationException("UserId is required for leadership slot.");

                cfg.UserId = req.UserId;
                cfg.DepartmentId = null;
            }
            // Các slot khác (nếu sau này mở rộng) cho phép cả 2 đều optional
            else
            {
                if (req.DepartmentId.HasValue)
                    cfg.DepartmentId = req.DepartmentId;

                if (req.UserId.HasValue)
                    cfg.UserId = req.UserId;
            }

            if (req.IsActive.HasValue)
                cfg.IsActive = req.IsActive.Value;

            await _db.SaveChangesAsync(ct);

            // Reload để lấy tên phòng & user
            var dep = cfg.DepartmentId.HasValue
                ? await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == cfg.DepartmentId.Value, ct)
                : null;

            var user = cfg.UserId.HasValue
                ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == cfg.UserId.Value, ct)
                : null;

            return ToDto(cfg, dep, user);
        }

        // ===== Helpers =====

        private static bool IsFunctionalDepartmentSlot(SlotKey slot)
            => slot == SlotKey.KHTH
               || slot == SlotKey.HCQT
               || slot == SlotKey.TCCB
               || slot == SlotKey.TCKT
               || slot == SlotKey.CTCD;

        private static bool IsLeadershipSlot(SlotKey slot)
            => slot == SlotKey.PGD1
               || slot == SlotKey.PGD2
               || slot == SlotKey.PGD3
               || slot == SlotKey.GiamDoc;

        private static string GetSlotDisplayName(SlotKey slot) =>
            slot switch
            {
                SlotKey.NguoiTrinh => "Người trình",
                SlotKey.LanhDaoPhong => "Lãnh đạo phòng",
                SlotKey.DonViLienQuan => "Đơn vị liên quan",
                SlotKey.KHTH => "Phòng KHTH",
                SlotKey.HCQT => "Phòng HCQT",
                SlotKey.TCCB => "Phòng TCCB",
                SlotKey.TCKT => "Phòng TCKT",
                SlotKey.CTCD => "Phòng CTXH / Công đoàn",
                SlotKey.PGD1 => "Phó Giám đốc 1",
                SlotKey.PGD2 => "Phó Giám đốc 2",
                SlotKey.PGD3 => "Phó Giám đốc 3",
                SlotKey.VanThuCheck => "Văn thư",
                SlotKey.GiamDoc => "Giám đốc",
                _ => slot.ToString()
            };

        private static SystemConfigItemDto ToDto(SystemConfig cfg, Department? dep, User? user)
            => new(
                Id: cfg.Id,
                Slot: cfg.Slot,
                SlotDisplayName: GetSlotDisplayName(cfg.Slot),
                IsFunctionalDepartment: IsFunctionalDepartmentSlot(cfg.Slot),
                DepartmentId: cfg.DepartmentId,
                DepartmentCode: dep?.Code,
                DepartmentName: dep?.Name,
                UserId: cfg.UserId,
                UserFullName: user?.FullName,
                IsActive: cfg.IsActive
            );
    }
}
