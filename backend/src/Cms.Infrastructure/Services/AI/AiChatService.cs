using System.Text;
using System.Text.Json;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Infrastructure.Http;
using Microsoft.Extensions.Configuration;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// FR-CIT-07 support chat. A fixed FAQ works offline; an optional LLM can improve
/// wording but cannot change data, call a decision gate or perform any action.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    private const int MaxMessageLength = 2000;
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private const string Disclaimer =
        "Information only. This chat does not submit, approve, reject or change any request.";

    private static readonly IReadOnlyList<FaqEntry> Faq =
        new[]
        {
            new FaqEntry(
                new[] { "submit", "request", "help", "start" },
                "A citizen can submit a request from the citizen portal. Include the category, " +
                "urgency, a short description and the location. A responder verifies it before " +
                "AI classification is recorded for human review.",
                new[] { "How do I check my request status?", "Who makes the final decision?" }),
            new FaqEntry(
                new[] { "status", "notification", "inbox", "bell", "track" },
                "Request status changes create a notification. Open the notification bell or " +
                "inbox to see messages delivered to your account.",
                new[] { "How are duplicate requests handled?", "What does Recommended mean?" }),
            new FaqEntry(
                new[] { "decide", "approve", "reject", "decision", "who" },
                "AI only suggests recommendations. An authorised human records an Approved, " +
                "Modified, Rejected or Deferred decision, and only that recorded decision can " +
                "authorise a critical action.",
                new[] { "What does Recommended mean?", "How do I submit a request?" }),
            new FaqEntry(
                new[] { "duplicate", "same", "again", "merge" },
                "Similar recent requests can appear as duplicate candidates for a human to " +
                "review. The system links candidates only; it never deletes or merges a request " +
                "from AI output alone.",
                new[] { "Who makes the final decision?", "How do I check my request status?" }),
            new FaqEntry(
                new[] { "emergency", "ambulance", "fire", "police", "danger" },
                "If anyone is in immediate danger, contact local emergency services first. " +
                "This portal is for coordinating aid and is not an emergency call service.",
                new[] { "How do I submit a request?", "Who makes the final decision?" })
        };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AiChatService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient(HttpClientExtensions.CmsExternalClientName);
        _configuration = configuration;
    }

    public async Task<AiChatResponseDto> AnswerAsync(
        AiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var question = RuleBasedProvider.ScrubPii(request.Message?.Trim() ?? string.Empty);
        if (question.Length == 0)
        {
            return RuleAnswer(
                "Please enter a question about submitting a request, tracking a request, " +
                "notifications or human decisions.",
                Array.Empty<string>());
        }

        if (question.Length > MaxMessageLength)
        {
            question = question[..MaxMessageLength];
        }

        var endpoint = _configuration["Ai:Chat:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            try
            {
                var answer = await AskLlmAsync(question, endpoint, cancellationToken);
                if (answer != null)
                {
                    return answer;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Optional informational upgrade timed out; FAQ still answers.
            }
            catch (Exception)
            {
                // A chat outage must never break the citizen portal.
            }
        }

        var match = Faq.FirstOrDefault(entry =>
            entry.Keywords.Any(keyword =>
                question.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        return match == null
            ? RuleAnswer(
                "I can explain how to submit a request, check status and notifications, " +
                "how human decisions work, and how duplicate candidates are handled.",
                new[] { "How do I submit a request?", "Who makes the final decision?" })
            : RuleAnswer(match.Answer, match.Suggestions);
    }

    private async Task<AiChatResponseDto?> AskLlmAsync(
        string question,
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CallTimeout);

        var body = JsonSerializer.Serialize(new { question });
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

        var answer = json.RootElement.TryGetProperty("answer", out var answerElement) &&
                     answerElement.ValueKind == JsonValueKind.String
            ? answerElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var suggestions = new List<string>();
        if (json.RootElement.TryGetProperty("suggestions", out var suggestionsElement) &&
            suggestionsElement.ValueKind == JsonValueKind.Array)
        {
            suggestions.AddRange(suggestionsElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(value => value.Length > 0)
                .Take(5));
        }

        return new AiChatResponseDto
        {
            Answer = Truncate(answer) + " " + Disclaimer,
            Source = Cms.Domain.Constants.RecommendationSources.Ai,
            Suggestions = suggestions,
            DecidesNothing = true
        };
    }

    private static AiChatResponseDto RuleAnswer(string answer, IReadOnlyList<string> suggestions) =>
        new()
        {
            Answer = answer + " " + Disclaimer,
            Source = Cms.Domain.Constants.RecommendationSources.Rule,
            Suggestions = suggestions,
            DecidesNothing = true
        };

    private static string Truncate(string value) =>
        value.Length <= 4000 ? value : value[..4000];

    private sealed record FaqEntry(
        IReadOnlyList<string> Keywords,
        string Answer,
        IReadOnlyList<string> Suggestions);
}
