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
                var actives = await _db.DigitalIdentities
                    .Where(x => x.UserId == req.UserId && x.IsActive)
                    .ToListAsync(ct);
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

                // GĐ5.1 – thông tin chứng thư mở rộng
                Provider = req.Provider,
                NotBefore = req.NotBefore,
                NotAfter = req.NotAfter,
                SerialNo = req.SerialNo,
                Issuer = req.Issuer,
                Subject = req.Subject
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

            // GĐ5.1 – cập nhật thông tin chứng thư (nếu gửi lên)
            if (req.Provider is not null) e.Provider = req.Provider;
            if (req.NotBefore.HasValue) e.NotBefore = req.NotBefore;
            if (req.NotAfter.HasValue) e.NotAfter = req.NotAfter;
            if (req.SerialNo is not null) e.SerialNo = req.SerialNo;
            if (req.Issuer is not null) e.Issuer = req.Issuer;
            if (req.Subject is not null) e.Subject = req.Subject;

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
                e.Id,
                e.UserId,
                e.EmpCode,
                e.CertName,
                e.Company,
                e.DisplayName,
                e.Title,
                e.IsActive,
                e.CreatedAtUtc,
                e.Provider,
                e.NotBefore,
                e.NotAfter,
                e.SerialNo,
                e.Issuer,
                e.Subject
            );
    }
}
