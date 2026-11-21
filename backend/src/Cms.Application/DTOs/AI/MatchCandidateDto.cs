namespace Cms.Application.DTOs.AI;

public class MatchCandidateDto
{
    public Guid TargetId { get; set; }

    public string TargetType { get; set; } = string.Empty;

    public double Score { get; set; }

    public string Rationale { get; set; } = string.Empty;

    public bool IsApproximate { get; set; }
}

public class MatchRequestDto
{
    public Guid TargetId { get; set; }

    /// <summary>Request, Resource, Shelter or Volunteer.</summary>
    public string TargetType { get; set; } = "Request";
}
