using System.Text;
using System.Text.Json;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities.Cases;
using Microsoft.Extensions.Configuration;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// Optional external provider. When it is not configured, or when any call fails,
/// <see cref="AiProviderSelector"/> falls back to <see cref="RuleBasedProvider"/>.
/// </summary>
public sealed class LlmProvider : IAiProvider
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public LlmProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public string Name => "LlmProvider";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["Ai:Llm:Endpoint"]);

    public async Task<AiAnalysisResult> AnalyzeAsync(
        Request request,
        IReadOnlyList<Request> candidateRequests,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["Ai:Llm:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("LLM endpoint is not configured.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CallTimeout);

        var body = JsonSerializer.Serialize(new
        {
            category = request.Category.ToString(),
            urgency = request.Urgency.ToString(),
            text = RuleBasedProvider.ScrubPii(
                $"{request.Category} {request.Urgency} {request.Description}")
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var apiKey = _configuration["Ai:Llm:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            content.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        }

        using var response = await _httpClient.PostAsync(endpoint, content, timeout.Token);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);

        var suggestionsElement = json.RootElement.TryGetProperty("suggestions", out var array)
            ? array
            : json.RootElement;

        var suggestions = new List<AiSuggestionDto>();

        if (suggestionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in suggestionsElement.EnumerateArray())
            {
                var type = ReadString(item, "type");
                var source = ReadString(item, "source");

                if (!RecommendationTypes.All.Contains(type, StringComparer.Ordinal) ||
                    !RecommendationSources.All.Contains(source, StringComparer.Ordinal))
                {
                    // Never allow an external provider to invent contract values.
                    continue;
                }

                suggestions.Add(new AiSuggestionDto
                {
                    Type = type,
                    TargetId = ReadGuid(item, "targetId", request.Id),
                    PayloadJson = ReadString(item, "payloadJson", "{}"),
                    Confidence = ReadDecimal(item, "confidence"),
                    Rationale = ReadString(item, "rationale"),
                    Source = source
                });
            }
        }

        return new AiAnalysisResult
        {
            Source = RecommendationSources.Ai,
            Suggestions = suggestions
        };
    }

    private static string ReadString(JsonElement element, string name, string fallback = "")
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static Guid ReadGuid(JsonElement element, string name, Guid fallback)
    {
        return element.TryGetProperty(name, out var value) &&
               Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : fallback;
    }

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) &&
               value.TryGetDecimal(out var parsed)
            ? Math.Clamp(parsed, 0m, 1m)
            : 0.5m;
    }
}
