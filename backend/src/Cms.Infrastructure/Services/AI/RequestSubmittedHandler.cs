using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// Reacts to the frozen Request.Submitted event. AI failures are swallowed here:
/// request submission and citizen flows are never blocked by decision support.
/// </summary>
public sealed class RequestSubmittedHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CmsDbContext _context;
    private readonly IAiProvider _aiProvider;
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<RequestSubmittedHandler> _logger;

    public RequestSubmittedHandler(
        CmsDbContext context,
        IAiProvider aiProvider,
        IRecommendationService recommendationService,
        ILogger<RequestSubmittedHandler> logger)
    {
        _context = context;
        _aiProvider = aiProvider;
        _recommendationService = recommendationService;
        _logger = logger;
    }

    public string Type => OutboxEventTypes.RequestSubmitted;

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        RequestSubmittedPayloadDto? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RequestSubmittedPayloadDto>(
                message.Payload,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid Request.Submitted payload.");
            return;
        }

        if (payload == null || payload.RequestId == Guid.Empty)
        {
            _logger.LogWarning("Invalid Request.Submitted payload (missing RequestId).");
            return;
        }

        try
        {
            var request = await _context.Requests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payload.RequestId, cancellationToken);

            if (request == null)
            {
                _logger.LogWarning(
                    "Request {RequestId} no longer exists; skipping AI triage.",
                    payload.RequestId);
                return;
            }

            var candidates = await _context.Requests
                .AsNoTracking()
                .Where(r => r.Id != request.Id && r.Status != Cms.Domain.Enums.Cases.RequestStatus.Closed)
                .OrderByDescending(r => r.SubmittedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            var analysis = await _aiProvider.AnalyzeAsync(request, candidates, cancellationToken);

            foreach (var suggestion in analysis.Suggestions)
            {
                await _recommendationService.CreateAsync(
                    new CreateRecommendationRequest
                    {
                        Type = suggestion.Type,
                        TargetId = suggestion.TargetId,
                        PayloadJson = suggestion.PayloadJson,
                        Confidence = suggestion.Confidence,
                        Rationale = suggestion.Rationale,
                        Source = suggestion.Source
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AI triage failed for request {RequestId}.", payload.RequestId);
        }
    }
}
