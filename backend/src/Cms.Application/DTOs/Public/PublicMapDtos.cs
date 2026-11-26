using System.ComponentModel.DataAnnotations;

namespace Cms.Application.DTOs.Public;

/// <summary>
/// Anonymous live-map projection. It contains only the fields that are safe to
/// disclose; requests, volunteers, internal ownership identifiers and private
/// alert text are never part of this contract. The incident Id is the public
/// key used by the map UI and is not a requester or volunteer identifier.
/// </summary>
public sealed record PublicMapDto(
    DateTime UpdatedAt,
    IReadOnlyList<PublicMapIncidentDto> Incidents,
    IReadOnlyList<PublicMapShelterDto> Shelters,
    IReadOnlyList<PublicMapAidLocationDto> AidLocations);

public sealed record PublicMapIncidentDto(
    Guid Id,
    string? Title,
    string? Summary,
    string? Type,
    string? Severity,
    string Status,
    string? District,
    double? Latitude,
    double? Longitude,
    DateTime? PublishedAt,
    DateTime? ExpiresAt);

public sealed record PublicMapShelterDto(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    int Capacity,
    int Occupancy,
    string Services);

public sealed record PublicMapAidLocationDto(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Services,
    string? OpeningHours);

/// <summary>
/// Coordinator input for the explicit alert publication layer. Validation only
/// applies when the alert is being made visible; hiding never requires public
/// content to be resubmitted.
/// </summary>
public sealed class PublishMapAlertDto : IValidatableObject
{
    public bool Visible { get; set; }

    public string? PublicTitle { get; set; }

    public string? PublicSummary { get; set; }

    public string? CrisisType { get; set; }

    public string? Severity { get; set; }

    public string? District { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Visible)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(PublicTitle))
        {
            yield return new ValidationResult("Public title is required.", [nameof(PublicTitle)]);
        }
        else if (PublicTitle.Trim().Length > 200)
        {
            yield return new ValidationResult("Public title must be 200 characters or fewer.", [nameof(PublicTitle)]);
        }

        if (string.IsNullOrWhiteSpace(PublicSummary))
        {
            yield return new ValidationResult("Public summary is required.", [nameof(PublicSummary)]);
        }
        else if (PublicSummary.Trim().Length > 1000)
        {
            yield return new ValidationResult("Public summary must be 1000 characters or fewer.", [nameof(PublicSummary)]);
        }

        if (string.IsNullOrWhiteSpace(CrisisType))
        {
            yield return new ValidationResult("Crisis type is required.", [nameof(CrisisType)]);
        }
        else if (CrisisType.Trim().Length > 40)
        {
            yield return new ValidationResult("Crisis type must be 40 characters or fewer.", [nameof(CrisisType)]);
        }

        if (Severity?.Trim().Length > 30)
        {
            yield return new ValidationResult("Severity must be 30 characters or fewer.", [nameof(Severity)]);
        }

        if (District?.Trim().Length > 120)
        {
            yield return new ValidationResult("District must be 120 characters or fewer.", [nameof(District)]);
        }

        if (!Latitude.HasValue)
        {
            yield return new ValidationResult("Latitude is required.", [nameof(Latitude)]);
        }
        else if (Latitude.Value < -90 || Latitude.Value > 90)
        {
            yield return new ValidationResult("Latitude must be between -90 and 90.", [nameof(Latitude)]);
        }

        if (!Longitude.HasValue)
        {
            yield return new ValidationResult("Longitude is required.", [nameof(Longitude)]);
        }
        else if (Longitude.Value < -180 || Longitude.Value > 180)
        {
            yield return new ValidationResult("Longitude must be between -180 and 180.", [nameof(Longitude)]);
        }
    }
}

public sealed class SetServiceCentreVisibilityDto
{
    public bool IsPublic { get; set; }
}
