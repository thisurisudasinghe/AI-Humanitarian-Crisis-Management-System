namespace Cms.Application.DTOs.AI;

public class AiSuggestionDto
{
    /// <summary>One of the frozen RecommendationType values.</summary>
    public string Type { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public decimal Confidence { get; set; }

    public string Rationale { get; set; } = string.Empty;

    /// <summary>Ai or Rule (frozen values).</summary>
    public string Source { get; set; } = string.Empty;
}

public class AiAnalysisResult
{
    public IReadOnlyList<AiSuggestionDto> Suggestions { get; set; } =
        Array.Empty<AiSuggestionDto>();

    public string Source { get; set; } = string.Empty;
}
