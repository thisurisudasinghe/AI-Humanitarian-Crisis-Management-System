namespace Cms.Application.DTOs.AI;

/// <summary>
/// Informational FAQ chat request. The response never executes an action.
/// </summary>
public sealed class AiChatRequestDto
{
    public string Message { get; set; } = string.Empty;
}

public sealed class AiChatResponseDto
{
    public string Answer { get; set; } = string.Empty;

    /// <summary>Rule or Ai.</summary>
    public string Source { get; set; } = string.Empty;

    public IReadOnlyList<string> Suggestions { get; set; } = Array.Empty<string>();

    public bool DecidesNothing { get; set; } = true;

    /// <summary>
    /// Clear provider/configuration or fallback notice for the UI. Empty when
    /// the configured AI provider answered successfully.
    /// </summary>
    public string Notice { get; set; } = string.Empty;
}
