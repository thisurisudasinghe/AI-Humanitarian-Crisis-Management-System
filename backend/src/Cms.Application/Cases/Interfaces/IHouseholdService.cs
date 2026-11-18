using Cms.Application.Cases.DTOs;

namespace Cms.Application.Cases.Interfaces;

public interface IHouseholdService
{
    Task<HouseholdDto?> GetMineAsync(CancellationToken cancellationToken = default);
    Task<Guid> UpsertMineAsync(SaveHouseholdDto request, CancellationToken cancellationToken = default);
}
