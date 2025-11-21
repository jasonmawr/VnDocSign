using Microsoft.EntityFrameworkCore;
using VnDocSign.Application.Contracts.Dtos.Users;
using VnDocSign.Application.Contracts.Interfaces.Users;
using VnDocSign.Domain.Entities.Core;
using VnDocSign.Infrastructure.Persistence;

namespace VnDocSign.Infrastructure.Services
{
    public sealed class UserDelegationService : IUserDelegationService
    {
        private readonly AppDbContext _db;

        public UserDelegationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<UserDelegationDto>> GetOwnedAsync(Guid ownerUserId, CancellationToken ct = default)
        {
            return await _db.UserDelegations
                .AsNoTracking()
                .Include(d => d.FromUser)
                .Include(d => d.ToUser)
                .Where(d => d.FromUserId == ownerUserId)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Select(d => ToDto(d))
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<UserDelegationDto>> GetDelegatedToMeAsync(Guid userId, CancellationToken ct = default)
        {
            return await _db.UserDelegations
                .AsNoTracking()
                .Include(d => d.FromUser)
                .Include(d => d.ToUser)
                .Where(d => d.ToUserId == userId)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Select(d => ToDto(d))
                .ToListAsync(ct);
        }

        public async Task<UserDelegationDto> CreateAsync(
            Guid ownerUserId,
            UserDelegationCreateRequest req,
            CancellationToken ct = default)
        {
            if (req.ToUserId == ownerUserId)
                throw new InvalidOperationException("Không thể ủy quyền cho chính mình.");

            var owner = await _db.Users.FirstOrDefaultAsync(u => u.Id == ownerUserId && u.IsActive, ct)
                        ?? throw new KeyNotFoundException("Người ủy quyền không tồn tại hoặc không hoạt động.");

            var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.ToUserId && u.IsActive, ct)
                         ?? throw new KeyNotFoundException("Người được ủy quyền không tồn tại hoặc không hoạt động.");

            var start = req.StartUtc;
            var end = req.EndUtc;

            if (end.HasValue && end.Value < start)
                throw new InvalidOperationException("Thời gian kết thúc phải lớn hơn hoặc bằng thời gian bắt đầu.");

            var now = DateTime.UtcNow;

            var entity = new UserDelegation
            {
                Id = Guid.NewGuid(),
                FromUserId = ownerUserId,
                ToUserId = req.ToUserId,
                StartUtc = start,
                EndUtc = end,
                IsActive = true,
                CreatedAtUtc = now
            };

            _db.UserDelegations.Add(entity);
            await _db.SaveChangesAsync(ct);

            // load kèm user name
            entity.FromUser = owner;
            entity.ToUser = target;

            return ToDto(entity);
        }

        public async Task<UserDelegationDto> UpdateAsync(
            Guid ownerUserId,
            Guid delegationId,
            UserDelegationUpdateRequest req,
            CancellationToken ct = default)
        {
            var entity = await _db.UserDelegations
                .Include(d => d.FromUser)
                .Include(d => d.ToUser)
                .FirstOrDefaultAsync(d => d.Id == delegationId, ct)
                ?? throw new KeyNotFoundException("Bản ghi ủy quyền không tồn tại.");

            if (entity.FromUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không phải chủ ủy quyền này.");

            if (req.EndUtc.HasValue && req.EndUtc.Value < req.StartUtc)
                throw new InvalidOperationException("Thời gian kết thúc phải lớn hơn hoặc bằng thời gian bắt đầu.");

            entity.StartUtc = req.StartUtc;
            entity.EndUtc = req.EndUtc;
            entity.IsActive = req.IsActive;

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task DeleteAsync(Guid ownerUserId, Guid delegationId, CancellationToken ct = default)
        {
            var entity = await _db.UserDelegations
                .FirstOrDefaultAsync(d => d.Id == delegationId, ct)
                ?? throw new KeyNotFoundException("Bản ghi ủy quyền không tồn tại.");

            if (entity.FromUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không phải chủ ủy quyền này.");

            _db.UserDelegations.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }

        private static UserDelegationDto ToDto(UserDelegation d)
            => new(
                d.Id,
                d.FromUserId,
                d.ToUserId,
                d.FromUser?.FullName,
                d.ToUser?.FullName,
                d.StartUtc,
                d.EndUtc,
                d.IsActive
            );
    }
}
