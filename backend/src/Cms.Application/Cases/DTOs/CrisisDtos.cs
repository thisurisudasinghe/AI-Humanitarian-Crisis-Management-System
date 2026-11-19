using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cms.Domain.Enums.Cases;

namespace Cms.Application.Cases.DTOs;

public class CrisisDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public CrisisStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class CreateCrisisDto
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Type { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public DateTime StartedAt { get; set; }
}

public class UpdateCrisisDto
{
    [StringLength(200)] public string? Name { get; set; }
    [StringLength(30)] public string? Type { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))] public CrisisStatus? Status { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class AreaDto
{
    public Guid Id { get; set; }
    public Guid CrisisId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public decimal? RadiusKm { get; set; }
}

public class CreateAreaDto
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Range(-90, 90)] public decimal Lat { get; set; }
    [Range(-180, 180)] public decimal Lng { get; set; }
    [Range(0, 1000)] public decimal? RadiusKm { get; set; }
}

public class AlertDto
{
    public Guid Id { get; set; }
    public Guid? CrisisId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))] public AlertStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAlertDto
{
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Body { get; set; } = string.Empty;
    public bool Publish { get; set; }
}

public class UpdateAlertDto
{
    [StringLength(200)] public string? Title { get; set; }
    [StringLength(2000)] public string? Body { get; set; }
    public bool? Publish { get; set; }
}

public class ServiceCentreDto
{
    public Guid Id { get; set; }
    public Guid? CrisisId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public string Services { get; set; } = string.Empty;
    public string? OpeningHours { get; set; }
}

public class CrisisSummaryDto
{
    public Guid CrisisId { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByUrgency { get; set; } = new();
    public Dictionary<string, int> ByArea { get; set; } = new();
    public int OpenTotal { get; set; }
    public int OverdueTotal { get; set; }
}

public class HouseholdDto
{
    public Guid Id { get; set; }
    public int Size { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public string? ContactPhone { get; set; }
}

public class SaveHouseholdDto
{
    [Range(1, 100)] public int Size { get; set; } = 1;
    public string? AccessibilityNeeds { get; set; }
    public string? ContactPhone { get; set; }
}
