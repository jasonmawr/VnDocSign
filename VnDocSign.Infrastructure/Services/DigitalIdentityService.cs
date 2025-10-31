using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.DigitalIdentities;
using VnDocSign.Application.Contracts.Interfaces.DigitalIdentities;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services
{
    public sealed class DigitalIdentityService : IDigitalIdentityService
    {
        private readonly AppDbContext _db;
        public DigitalIdentityService(AppDbContext db) => _db = db;

        public async Task<DigitalIdentityDto> GetAsync(Guid id, CancellationToken ct = default)
        {
            var e = await _db.DigitalIdentities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException("DigitalIdentity not found.");
            return Map(e);
        }

        public async Task<IReadOnlyList<DigitalIdentityDto>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        {
            var list = await _db.DigitalIdentities.AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreatedAtUtc)
                .ToListAsync(ct);
            return list.Select(Map).ToList();
        }

        public async Task<DigitalIdentityDto> CreateAsync(DigitalIdentityCreateRequest req, CancellationToken ct = default)
        {
            // Đảm bảo chỉ 1 DigitalIdentity active cho mỗi User
            if (req.IsActive)
            {
                var actives = await _db.DigitalIdentities.Where(x => x.UserId == req.UserId && x.IsActive).ToListAsync(ct);
                foreach (var a in actives) a.IsActive = false;
            }

            var e = new VnDocSign.Domain.Entities.Core.DigitalIdentity
            {
                Id = Guid.NewGuid(),
                UserId = req.UserId,
                EmpCode = req.EmpCode,
                CertName = req.CertName,
                Company = req.Company,
                DisplayName = req.DisplayName,
                Title = req.Title,
                IsActive = req.IsActive,
                CreatedAtUtc = DateTime.UtcNow,

                // Nếu bạn đã làm GĐ5.1 mở rộng entity thì map thêm các trường mới:
                Provider = (req as dynamic)?.Provider,
                NotBefore = (req as dynamic)?.NotBefore,
                NotAfter = (req as dynamic)?.NotAfter,
                SerialNo = (req as dynamic)?.SerialNo,
                Issuer = (req as dynamic)?.Issuer,
                Subject = (req as dynamic)?.Subject
            };

            _db.DigitalIdentities.Add(e);
            await _db.SaveChangesAsync(ct);
            return Map(e);
        }

        public async Task<DigitalIdentityDto> UpdateAsync(Guid id, DigitalIdentityUpdateRequest req, CancellationToken ct = default)
        {
            var e = await _db.DigitalIdentities.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException("DigitalIdentity not found.");

            if (req.IsActive is true)
            {
                var actives = await _db.DigitalIdentities
                    .Where(x => x.UserId == e.UserId && x.IsActive && x.Id != e.Id)
                    .ToListAsync(ct);
                foreach (var a in actives) a.IsActive = false;
            }

            e.EmpCode = req.EmpCode ?? e.EmpCode;
            e.CertName = req.CertName ?? e.CertName;
            e.Company = req.Company ?? e.Company;
            e.DisplayName = req.DisplayName ?? e.DisplayName;
            e.Title = req.Title ?? e.Title;
            if (req.IsActive.HasValue) e.IsActive = req.IsActive.Value;

            // Nếu đã mở rộng entity (GĐ5.1), cập nhật các trường mới (an toàn với null):
            var dyn = (req as dynamic);
            e.Provider = dyn?.Provider ?? e.Provider;
            e.NotBefore = dyn?.NotBefore ?? e.NotBefore;
            e.NotAfter = dyn?.NotAfter ?? e.NotAfter;
            e.SerialNo = dyn?.SerialNo ?? e.SerialNo;
            e.Issuer = dyn?.Issuer ?? e.Issuer;
            e.Subject = dyn?.Subject ?? e.Subject;

            await _db.SaveChangesAsync(ct);
            return Map(e);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var e = await _db.DigitalIdentities.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException("DigitalIdentity not found.");
            _db.DigitalIdentities.Remove(e);
            await _db.SaveChangesAsync(ct);
        }

        private static DigitalIdentityDto Map(VnDocSign.Domain.Entities.Core.DigitalIdentity e)
            => new(
                e.Id, e.UserId, e.EmpCode, e.CertName, e.Company,
                e.DisplayName, e.Title, e.IsActive, e.CreatedAtUtc,
                // Nếu DTO của bạn đã mở rộng GĐ5.1, map các trường mới; nếu chưa, các tham số cuối có thể bỏ:
                e.Provider, e.NotBefore, e.NotAfter, e.SerialNo, e.Issuer, e.Subject
            );
    }
}
