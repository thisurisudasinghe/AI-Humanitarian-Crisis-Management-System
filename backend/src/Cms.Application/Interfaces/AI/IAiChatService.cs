using Cms.Application.DTOs.AI;

namespace Cms.Application.Interfaces.AI;

/// <summary>
/// Fixed FAQ support chat with an optional informational LLM upgrade. It never
/// classifies, approves, allocates, rejects or otherwise decides anything.
/// </summary>
public interface IAiChatService
{
    Task<AiChatResponseDto> AnswerAsync(
        AiChatRequestDto request,
        CancellationToken cancellationToken = default);
}
