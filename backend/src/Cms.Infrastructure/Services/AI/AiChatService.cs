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
        "Information only. This chat does not submit, approve, reject or change any request, " +
        "and it does not provide live shelter availability, inventory quantities or operational status. " +
        "If anyone is in immediate danger, contact local emergency services.";
    private const string NotConfiguredNotice =
        "AI provider is not configured. Showing SAFEWAY's verified FAQ guidance.";
    private const string ProviderUnavailableNotice =
        "The AI provider is temporarily unavailable. Showing SAFEWAY's verified FAQ guidance.";
    private const string LlmInstruction =
        "You are the SAFEWAY informational assistant. Answer only general questions about how " +
        "the platform works. Never claim live emergency information, shelter availability, " +
        "inventory quantities or operational status. Never submit, approve, reject or change a " +
        "request, resource, shelter or decision. For immediate danger, tell the user to contact " +
        "local emergency services. Direct operational decisions to authorised human staff.";

    private static readonly IReadOnlyList<FaqEntry> Faq =
        new[]
        {
            new FaqEntry(
                new[] { "emergency", "ambulance", "fire", "police", "danger", "immediate", "urgent", "life threatening" },
                "If anyone is in immediate danger, contact your local emergency services first. " +
                "SAFEWAY is for coordinating aid and is not an emergency call service. " +
                "After everyone is safe, you can report the incident for human triage.",
                new[] { "How do I report an incident?", "How do I find assistance?" }),
            new FaqEntry(
                new[] { "report", "incident", "submit", "request", "start", "new" },
                "Use Report to submit a request or incident. Include the category, urgency, " +
                "a short description and the location. A responder verifies it before AI " +
                "classification is recorded for human review. Use the official forms for " +
                "sensitive details rather than this chat.",
                new[] { "How do I check my request status?", "Who makes the final decision?" }),
            new FaqEntry(
                new[] { "shelter", "accommodation", "housing", "bed", "sleep" },
                "Open the Shelters page for the current verified shelter information maintained " +
                "by authorised staff. This chat cannot confirm bed availability, eligibility or " +
                "operational status. For urgent placement, contact the listed service centre " +
                "or local emergency services.",
                new[] { "How do I find assistance?", "How do I report an incident?" }),
            new FaqEntry(
                new[] { "resource", "resources", "inventory", "supply", "supplies", "stock" },
                "The Resources page shows inventory, matching and dispatch information maintained " +
                "by authorised logistics staff. This chat does not display live quantities and " +
                "cannot allocate supplies. Ask a coordinator or logistics manager about a " +
                "specific operational request.",
                new[] { "Who makes the final decision?", "How does the workflow work?" }),
            new FaqEntry(
                new[] { "volunteer", "volunteering", "join", "sign up", "activity", "activities" },
                "Use the Volunteers page to register a profile, record availability and see " +
                "assigned activities. A volunteer coordinator verifies profiles and assigns " +
                "tasks; the AI assistant cannot approve volunteer status.",
                new[] { "How do I sign up to volunteer?", "How can I donate?" }),
            new FaqEntry(
                new[] { "donat", "donation", "donations", "give", "fund", "funding", "money" },
                "The Donations page explains how to support SAFEWAY and how contributions are " +
                "recorded by authorised staff. Do not send card numbers or bank details to " +
                "this chat. For a specific donation question, contact the donations team.",
                new[] { "How do I volunteer?", "How do I find assistance?" }),
            new FaqEntry(
                new[] { "assistance", "help", "aid", "support", "find", "service centre", "services" },
                "Use Find assistance to see published alerts, active service centres and safety " +
                "guidance. Availability and operational status are maintained by authorised " +
                "staff; this chat does not create a request or confirm live capacity. If danger " +
                "is immediate, contact local emergency services.",
                new[] { "How do I find a shelter?", "How do I report an incident?" }),
            new FaqEntry(
                new[] { "status", "track", "notification", "notifications", "inbox", "bell", "update" },
                "Request status changes create a notification. Open the notification bell or " +
                "inbox to see messages delivered to your account. Signed-in users can also " +
                "track submissions from My Reports.",
                new[] { "How are duplicate requests handled?", "What does Recommended mean?" }),
            new FaqEntry(
                new[] { "decide", "decision", "approve", "approval", "reject", "who", "final", "authorise", "authorize" },
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
                new[] { "workflow", "process", "triage", "triaged", "recommended", "allocated", "dispatched", "fulfilled", "stage", "lifecycle" },
                "A request normally moves from Submitted to Triaged, then Recommended after an " +
                "approved match, then Allocated, Dispatched and Fulfilled. AI can suggest at " +
                "each stage, but only authorised human decisions unlock critical steps.",
                new[] { "Who makes the final decision?", "How do I check my request status?" }),
            new FaqEntry(
                new[] { "login", "log in", "register", "registration", "account", "password", "role" },
                "Use Login for an existing account or Register to create one. After signing in, " +
                "your dashboard and navigation reflect your assigned role. Role changes are " +
                "managed by authorised administrators, not by this chat.",
                new[] { "How do I submit a request?", "How do I check my request status?" }),
            new FaqEntry(
                new[] { "privacy", "private", "personal", "data", "secure", "security", "scrub" },
                "Do not share personal, medical or payment details in this chat. The backend " +
                "scrubs personal details before an optional AI call and this assistant never " +
                "changes a record. Use the official SAFEWAY forms for sensitive information.",
                new[] { "How do I submit a request?", "Who makes the final decision?" }),
            new FaqEntry(
                new[] { "use", "using", "safeway", "platform", "portal", "guide", "faq", "question" },
                "SAFEWAY helps you report incidents, find published assistance and coordinate a " +
                "verified human-led response. Start with Report, Find assistance and Services, " +
                "then use your dashboard after signing in. I answer questions only.",
                new[] { "How do I report an incident?", "How do I find assistance?" })
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

        var endpoint = _configuration["Ai:Chat:Endpoint"];
        var providerConfigured = !string.IsNullOrWhiteSpace(endpoint);
        var notice = providerConfigured ? string.Empty : NotConfiguredNotice;

        var question = RuleBasedProvider.ScrubPii(request.Message?.Trim() ?? string.Empty);
        if (question.Length == 0)
        {
            return RuleAnswer(
                "Please enter a question about reporting an incident, finding assistance, " +
                "shelters, resources, volunteering, donations or human decisions.",
                Array.Empty<string>(),
                notice);
        }

        if (question.Length > MaxMessageLength)
        {
            question = question[..MaxMessageLength];
        }

        if (providerConfigured)
        {
            try
            {
                var answer = await AskLlmAsync(question, endpoint!, cancellationToken);
                if (answer != null)
                {
                    return answer;
                }

                notice = ProviderUnavailableNotice;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Optional informational upgrade timed out; FAQ still answers.
                notice = ProviderUnavailableNotice;
            }
            catch (Exception)
            {
                // A chat outage must never break the citizen portal.
                notice = ProviderUnavailableNotice;
            }
        }

        var match = Faq
            .Select(entry => new
            {
                Entry = entry,
                Score = entry.Keywords.Count(keyword =>
                    question.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();

        return match == null
            ? RuleAnswer(
                "I can explain how to report an incident, find assistance and shelters, " +
                "understand resources and the response workflow, volunteer, donate, and " +
                "how authorised people make decisions.",
                new[] { "How do I report an incident?", "How do I find assistance?" },
                notice)
            : RuleAnswer(match.Entry.Answer, match.Entry.Suggestions, notice);
    }

    private async Task<AiChatResponseDto?> AskLlmAsync(
        string question,
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CallTimeout);

        var body = JsonSerializer.Serialize(new { question, instructions = LlmInstruction });
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
            DecidesNothing = true,
            Notice = string.Empty
        };
    }

    private static AiChatResponseDto RuleAnswer(
        string answer,
        IReadOnlyList<string> suggestions,
        string notice) =>
        new()
        {
            Answer = answer + " " + Disclaimer,
            Source = Cms.Domain.Constants.RecommendationSources.Rule,
            Suggestions = suggestions,
            DecidesNothing = true,
            Notice = notice
        };

    private static string Truncate(string value) =>
        value.Length <= 4000 ? value : value[..4000];

    private sealed record FaqEntry(
        IReadOnlyList<string> Keywords,
        string Answer,
        IReadOnlyList<string> Suggestions);
}
