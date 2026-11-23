using Cms.Application.Contracts;

namespace Cms.Application.Operations;

/// <summary>Shared paged envelope for all Member 4 list endpoints.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Area markers exposed to every role that can open the operations map.</summary>
public sealed record MapAreaDto(
    Guid Id, Guid CrisisId, string Name, decimal Lat, decimal Lng, decimal? RadiusKm);

public sealed record ResourceTypeDto(Guid Id, string Name, string Unit, string? Description);

public sealed record InventoryDto(
    Guid Id,
    Guid ResourceTypeId,
    Guid OrgId,
    decimal Quantity,
    decimal Available,
    decimal Reserved,
    decimal Allocated,
    decimal Dispatched,
    decimal Utilised,
    decimal ReorderThreshold,
    DateTime? ExpiryDate,
    double? Lat,
    double? Lng,
    bool AccessibilityCapable,
    bool LowStock);

public sealed record AllocationDto(
    Guid Id,
    Guid RequestId,
    Guid InventoryId,
    decimal Quantity,
    string Status,
    Guid DecisionId,
    DateTime AllocatedAt);

public sealed record DispatchDto(
    Guid Id,
    Guid AllocationId,
    Guid DecisionId,
    decimal Quantity,
    string Status,
    DateTime? ConfirmedAt,
    Guid? ConfirmedBy);

public sealed record ShelterDto(
    Guid Id,
    string Name,
    int Capacity,
    int Occupancy,
    int FreePlaces,
    string Facilities,
    string AccessibilityFeatures,
    string Services,
    double Lat,
    double Lng,
    Guid OrgId,
    bool Approved);

public sealed record ShelterAllocationDto(
    Guid Id,
    Guid RequestId,
    Guid ShelterId,
    int HouseholdSize,
    Guid DecisionId,
    DateTime CreatedAt);

public sealed record VolunteerDto(
    Guid Id,
    Guid UserId,
    string SkillsJson,
    string LanguagesJson,
    string VerificationStatus,
    bool Active,
    double? Lat,
    double? Lng);

public sealed record VolunteerAvailabilityDto(
    Guid Id,
    Guid VolunteerId,
    DateTime StartsAt,
    DateTime EndsAt);

public sealed record VolunteerTaskDto(
    Guid Id,
    Guid? RequestId,
    string Title,
    string RequiredSkillsJson,
    double? Lat,
    double? Lng,
    DateTime? ScheduledAt,
    string Status,
    Guid? VolunteerId,
    DateTime? CompletedAt);

/// <summary>Caller-scoped read model for the public volunteer portal.</summary>
public sealed record VolunteerPortalDto(
    VolunteerDto? Profile,
    IReadOnlyList<VolunteerAvailabilityDto> Availability,
    int CompletedTasks,
    IReadOnlyList<VolunteerTaskDto> Opportunities);

public sealed record AssistanceRequestDto(
    Guid Id,
    Guid FromOrgId,
    Guid? ToOrgId,
    Guid ResourceTypeId,
    decimal Quantity,
    string Reason,
    string Status,
    Guid? ShelterId,
    string? ResponseReason,
    DateTime CreatedAt);

public sealed record ShortageProposalItemDto(
    Guid RequestId,
    string Category,
    string Urgency,
    decimal RequestedQuantity,
    decimal AvailableAtStart,
    int PriorityPosition,
    decimal ProposedPartialFulfilment,
    string Explanation);

public sealed record ShortageProposalDto(
    Guid ResourceTypeId,
    decimal AvailableQuantity,
    IReadOnlyList<ShortageProposalItemDto> Items,
    bool HasSideEffects,
    bool HumanApprovalRequired);

public sealed record PendingStatusTransitionDto(
    Guid Id,
    string Operation,
    Guid EntityId,
    Guid RequestId,
    string TargetStatus,
    int Attempts,
    string? LastError,
    DateTime? CompletedAt);

// ---------------------------------------------------------------------------
// Transport-independent input models. Entities are never bound directly.
// ---------------------------------------------------------------------------
public sealed record CreateResourceTypeInput(string Name, string Unit, string? Description);
public sealed record CreateInventoryInput(
    Guid ResourceTypeId,
    Guid OrgId,
    decimal Quantity,
    decimal ReorderThreshold,
    DateTime? ExpiryDate,
    double? Lat,
    double? Lng,
    bool AccessibilityCapable);
public sealed record UpdateInventoryInput(
    decimal Quantity,
    decimal ReorderThreshold,
    DateTime? ExpiryDate,
    double? Lat,
    double? Lng,
    bool AccessibilityCapable);
public sealed record CreateShelterInput(
    string Name,
    int Capacity,
    int Occupancy,
    string Facilities,
    string AccessibilityFeatures,
    string Services,
    double Lat,
    double Lng,
    Guid OrgId);
public sealed record UpdateShelterInput(
    string Name,
    int Capacity,
    string Facilities,
    string AccessibilityFeatures,
    string Services,
    double Lat,
    double Lng);
public sealed record SaveVolunteerProfileInput(
    string SkillsJson,
    string LanguagesJson,
    double? Lat,
    double? Lng,
    bool Active);
public sealed record AddAvailabilityInput(DateTime StartsAt, DateTime EndsAt);
public sealed record CreateVolunteerTaskInput(
    Guid? RequestId,
    string Title,
    string RequiredSkillsJson,
    double? Lat,
    double? Lng,
    DateTime? ScheduledAt);
public sealed record CreateAssistanceRequestInput(
    Guid FromOrgId,
    Guid? ToOrgId,
    Guid ResourceTypeId,
    decimal Quantity,
    string Reason,
    Guid? ShelterId);

/// <summary>
/// Single application boundary for the Member 4 vertical slice. Controllers stay
/// thin: every business rule, transaction and side effect lives behind this API.
/// </summary>
public interface IOperationsService
{
    Task<IReadOnlyList<ResourceTypeDto>> GetResourceTypesAsync(CancellationToken ct);
    Task<ResourceTypeDto> CreateResourceTypeAsync(CreateResourceTypeInput input, CancellationToken ct);

    Task<PagedResult<InventoryDto>> GetInventoriesAsync(
        Guid? orgId, Guid? resourceTypeId, Guid? areaId, bool? lowStock,
        int page, int pageSize, CancellationToken ct);
    Task<InventoryDto> CreateInventoryAsync(CreateInventoryInput input, CancellationToken ct);
    Task<InventoryDto> UpdateInventoryAsync(Guid id, UpdateInventoryInput input, CancellationToken ct);

    Task<AllocationDto> AllocateAsync(Guid decisionId, string idempotencyKey, CancellationToken ct);
    Task<PagedResult<AllocationDto>> GetAllocationsAsync(
        Guid? requestId, string? status, int page, int pageSize, CancellationToken ct);

    Task<DispatchDto> DispatchAsync(Guid decisionId, Guid? allocationId, CancellationToken ct);
    Task<DispatchDto> ConfirmDeliveryAsync(Guid decisionId, Guid? dispatchId, CancellationToken ct);
    Task<PagedResult<DispatchDto>> GetDispatchesAsync(
        Guid? allocationId, int page, int pageSize, CancellationToken ct);

    Task<PagedResult<ShelterDto>> GetSheltersAsync(
        bool? approved, Guid? orgId, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<ShelterDto>> GetPublicSheltersAsync(CancellationToken ct);
    Task<ShelterDto> CreateShelterAsync(CreateShelterInput input, CancellationToken ct);
    Task<ShelterDto> UpdateShelterAsync(Guid id, UpdateShelterInput input, CancellationToken ct);
    Task<ShelterDto> ApproveShelterAsync(Guid id, CancellationToken ct);
    Task<ShelterAllocationDto> AllocateShelterAsync(Guid decisionId, CancellationToken ct);

    Task<IReadOnlyList<VolunteerDto>> GetVolunteersAsync(CancellationToken ct);
    Task<VolunteerDto> SaveVolunteerProfileAsync(SaveVolunteerProfileInput input, CancellationToken ct);
    Task<VolunteerDto> VerifyVolunteerAsync(Guid id, bool approved, string? reason, CancellationToken ct);
    Task<VolunteerAvailabilityDto> AddAvailabilityAsync(AddAvailabilityInput input, CancellationToken ct);
    Task<PagedResult<VolunteerTaskDto>> GetMyTasksAsync(int page, int pageSize, CancellationToken ct);
    Task<VolunteerPortalDto> GetVolunteerPortalAsync(CancellationToken ct);
    Task<PagedResult<VolunteerTaskDto>> GetVolunteerTasksAsync(string? status, int page, int pageSize, CancellationToken ct);
    Task<VolunteerTaskDto> CreateVolunteerTaskAsync(CreateVolunteerTaskInput input, CancellationToken ct);
    Task<VolunteerTaskDto> AssignVolunteerTaskAsync(Guid id, Guid volunteerId, CancellationToken ct);
    Task<VolunteerTaskDto> RespondToTaskAsync(Guid id, bool accept, string? reason, CancellationToken ct);
    Task<VolunteerTaskDto> CompleteTaskAsync(Guid id, CancellationToken ct);

    Task<AssistanceRequestDto> CreateAssistanceRequestAsync(CreateAssistanceRequestInput input, CancellationToken ct);
    Task<AssistanceRequestDto> RespondAssistanceRequestAsync(Guid id, bool accept, string? reason, CancellationToken ct);
    Task<PagedResult<AssistanceRequestDto>> GetAssistanceRequestsAsync(
        string? direction, string? status, int page, int pageSize, CancellationToken ct);

    Task<ShortageProposalDto> GetShortageProposalAsync(Guid resourceTypeId, CancellationToken ct);

    Task<IReadOnlyList<PendingStatusTransitionDto>> GetPendingTransitionsAsync(CancellationToken ct);
    Task<PendingStatusTransitionDto?> RetryStatusTransitionAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<MatchCandidate>> RankResourcesAsync(Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<MatchCandidate>> RankSheltersAsync(Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<MatchCandidate>> RankVolunteersAsync(Guid requestId, CancellationToken ct);

    Task<IReadOnlyList<MapAreaDto>> GetMapAreasAsync(CancellationToken ct);
    Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct);
    Task<DistanceResult> GetDistanceAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct);
}
