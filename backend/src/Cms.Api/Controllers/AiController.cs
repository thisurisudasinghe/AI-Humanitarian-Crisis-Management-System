using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Informational AI endpoints. AI output is advisory and never performs a
/// critical action.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IAiChatService _chat;

    public AiController(IAiChatService chat)
    {
        _chat = chat;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await _chat.AnswerAsync(request, cancellationToken);
        return Ok(response);
    }
}
