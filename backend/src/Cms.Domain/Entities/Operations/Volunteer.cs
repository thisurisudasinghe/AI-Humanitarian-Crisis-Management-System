using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;

public class Volunteer : BaseEntity { public Guid UserId { get; set; } public string SkillsJson { get; set; } = "[]"; public string LanguagesJson { get; set; } = "[]"; public string VerificationStatus { get; set; } = "Pending"; public string? VerificationReason { get; set; } public bool Active { get; set; } = true; public double? Lat { get; set; } public double? Lng { get; set; } }
