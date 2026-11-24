using Cms.Application.Cases.DTOs;
using Cms.Application.DTOs.Public;

namespace Cms.Application.Cases.Interfaces;

public interface ICrisisService
{
    Task<IReadOnlyList<CrisisDto>> ListCrisesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateCrisisAsync(CreateCrisisDto request, CancellationToken cancellationToken = default);
    Task UpdateCrisisAsync(Guid crisisId, UpdateCrisisDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AreaDto>> ListAreasAsync(Guid? crisisId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAreaAsync(Guid crisisId, CreateAreaDto request, CancellationToken cancellationToken = default);
    Task<Guid> CreateAlertAsync(Guid crisisId, CreateAlertDto request, CancellationToken cancellationToken = default);
    Task UpdateAlertAsync(Guid alertId, UpdateAlertDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertPublicationDto>> ListAlertsForPublicationAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertDto>> ListPublicAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceCentreDto>> ListPublicServiceCentresAsync(CancellationToken cancellationToken = default);
    Task<CrisisSummaryDto> GetSummaryAsync(Guid crisisId, CancellationToken cancellationToken = default);

    // Explicit publication layer for the anonymous live map.
    Task PublishAlertToMapAsync(
        Guid alertId,
        PublishMapAlertDto request,
        CancellationToken cancellationToken = default);

    Task<PublicMapDto> GetPublicMapAsync(CancellationToken cancellationToken = default);

    Task SetServiceCentrePublicVisibilityAsync(
        Guid serviceCentreId,
        bool isPublic,
        CancellationToken cancellationToken = default);
}
