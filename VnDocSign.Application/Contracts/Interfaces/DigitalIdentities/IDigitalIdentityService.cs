using VnDocSign.Application.Contracts.Dtos.DigitalIdentities;

namespace VnDocSign.Application.Contracts.Interfaces.DigitalIdentities;

public interface IDigitalIdentityService
{
    Task<DigitalIdentityDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DigitalIdentityDto>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task<DigitalIdentityDto> CreateAsync(DigitalIdentityCreateRequest req, CancellationToken ct = default);
    Task<DigitalIdentityDto> UpdateAsync(Guid id, DigitalIdentityUpdateRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
