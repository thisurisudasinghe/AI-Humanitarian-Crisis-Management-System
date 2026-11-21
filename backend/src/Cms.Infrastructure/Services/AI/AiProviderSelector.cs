using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Entities.Cases;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// Tries the optional LLM provider first and always degrades to rule-based analysis.
/// </summary>
public sealed class AiProviderSelector : IAiProvider
{
    private readonly LlmProvider _llmProvider;
    private readonly RuleBasedProvider _ruleBasedProvider;
    private readonly ILogger<AiProviderSelector> _logger;

    public AiProviderSelector(
        LlmProvider llmProvider,
        RuleBasedProvider ruleBasedProvider,
        ILogger<AiProviderSelector> logger)
    {
        _llmProvider = llmProvider;
        _ruleBasedProvider = ruleBasedProvider;
        _logger = logger;
    }

    public string Name => "AiProviderSelector";

    public async Task<AiAnalysisResult> AnalyzeAsync(
        Request request,
        IReadOnlyList<Request> candidateRequests,
        CancellationToken cancellationToken = default)
    {
        if (_llmProvider.IsConfigured)
        {
            try
            {
                var result = await _llmProvider.AnalyzeAsync(
                    request,
                    candidateRequests,
                    cancellationToken);

                if (result.Suggestions.Count > 0 &&
                    result.Suggestions.All(suggestion => suggestion.Confidence >= 0.5m))
                {
                    return result;
                }

                _logger.LogWarning(
                    "LLM provider returned no suggestions or confidence below 0.5; using rule-based fallback.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The provider's 5s timeout must never block request submission.
                // A genuine caller cancellation is still propagated.
                _logger.LogWarning("LLM provider timed out; using rule-based fallback.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "LLM provider failed; using rule-based fallback.");
            }
        }

        return await _ruleBasedProvider.AnalyzeAsync(
            request,
            candidateRequests,
            cancellationToken);
    }
}
