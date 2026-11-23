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
    private static readonly string[] EmergencyKeywords =
    {
        "emergency", "ambulance", "fire", "police", "danger", "immediate", "life threatening",
        "life-threatening", "trapped", "bleeding", "unconscious", "collapsed", "call 911",
        "call 999", "call 119"
    };

    private sealed record FaqEntry(
        IReadOnlyList<string> Keywords,
        string Answer,
        IReadOnlyList<string> Suggestions);

    private sealed record LocalePack(
        string Disclaimer,
        string NotConfiguredNotice,
        string ProviderUnavailableNotice,
        string LlmInstruction,
        string EmergencyAnswer,
        string SafetyGuardAnswer,
        string EmptyQuestion,
        string DefaultAnswer,
        IReadOnlyList<string> DefaultSuggestions,
        IReadOnlyList<FaqEntry> Faq);

    private static readonly LocalePack En = new(
        "Information only. This chat does not submit, approve, reject or change any request, and it does not provide live shelter availability, inventory quantities or operational status. If anyone is in immediate danger, contact local emergency services.",
        "AI provider is not configured. Showing SAFEWAY's verified FAQ guidance.",
        "The AI provider is temporarily unavailable. Showing SAFEWAY's verified FAQ guidance.",
        "You are the SAFEWAY informational assistant. Answer only general questions about how the platform works. Never claim live emergency information, shelter availability, inventory quantities or operational status. Never submit, approve, reject or change a request, resource, shelter, volunteer assignment or decision. Never provide medical, legal or financial instructions beyond general SAFEWAY guidance. For immediate danger, tell the user to contact local emergency services. Direct operational decisions to authorised human staff. Respond in English.",
        "If anyone is in immediate danger, contact your local emergency services immediately. SAFEWAY can help coordinate aid after people are safe, but it is not an emergency call service. After everyone is safe, you can report the incident for human triage.",
        "I can provide informational guidance only. Authorised human staff must review and record any decision or operational action.",
        "Please enter a question about reporting an incident, finding assistance, shelters, resources, volunteering, donations, notifications or human decisions.",
        "I can explain how to report an incident, find assistance and shelters, understand resources and the response workflow, volunteer, donate, and how authorised people make decisions.",
        new[] { "How do I report an incident?", "How do I find assistance?" },
        BuildEnFaq());

    private static readonly LocalePack Si = new(
        "තොරතුරු සඳහා පමණි. මෙම කතාබහ මඟින් කිසිදු ඉල්ලීමක් ඉදිරිපත් කිරීම, අනුමත කිරීම, ප්‍රතික්ෂේප කිරීම හෝ වෙනස් කිරීම සිදු නොකරයි. සජීවී ආරක්ෂිත ස්ථාන ලබාගත හැකි බව, ඉන්වෙන්ටරි ප්‍රමාණ හෝ මෙහෙයුම් තත්ත්වය ලබා නොදේ. කිසිවෙකු ක්ෂණික අනතුරක සිටී නම්, දේශීය හදිසි සේවාවන් අමතන්න.",
        "AI සේවා සැපයුම්කරු සකසා නැත. SAFEWAY හි සත්‍යාපිත FAQ මාර්ගෝපදේශය පෙන්වයි.",
        "AI සේවා සැපයුම්කරු තාවකාලිකව ලබාගත නොහැක. SAFEWAY හි සත්‍යාපිත FAQ මාර්ගෝපදේශය පෙන්වයි.",
        "You are the SAFEWAY informational assistant. Answer only general questions about how the platform works. Never claim live emergency information, shelter availability, inventory quantities or operational status. Never submit, approve, reject or change a request, resource, shelter, volunteer assignment or decision. Never provide medical, legal or financial instructions beyond general SAFEWAY guidance. For immediate danger, tell the user to contact local emergency services. Direct operational decisions to authorised human staff. Respond in Sinhala.",
        "කිසිවෙකු ක්ෂණික අනතුරක සිටී නම්, පළමුව දේශීය හදිසි සේවාවන් අමතන්න. SAFEWAY හදිසි ඇමතුම් සේවාවක් නොවේ; එය සහාය සම්බන්ධීකරණය සඳහාය. සියල්ලන් ආරක්ෂිත වූ පසු, මානුෂීය සමාලෝචනය සඳහා සිදුවීම වාර්තා කළ හැක.",
        "මට තොරතුරු මාර්ගෝපදේශ පමණක් ලබා දිය හැක. ඕනෑම තීරණයක් හෝ මෙහෙයුම් ක්‍රියාවක් බලයලත් මානුෂීය කාර්ය මණ්ඩලය සමාලෝචනය කර වාර්තා කළ යුතුය.",
        "සිදුවීමක් වාර්තා කිරීම, සහාය සොයාගැනීම, ආරක්ෂිත ස්ථාන, සම්පත්, ස්වේච්ඡා සේවය, පරිත්‍යාග හෝ මානුෂීය තීරණ පිළිබඳ ප්‍රශ්නයක් ඇතුළත් කරන්න.",
        "සිදුවීමක් වාර්තා කරන ආකාරය, සහාය සහ ආරක්ෂිත ස්ථාන සොයාගැනීම, සම්පත් සහ ප්‍රතිචාර ක්‍රියාවලිය තේරුම් ගැනීම, ස්වේච්ඡා සේවය, පරිත්‍යාග සහ බලයලත් පුද්ගලයින් තීරණ ගන්නා ආකාරය පිළිබඳව මට පැහැදිලි කළ හැක.",
        new[] { "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?", "සහාය සොයාගන්නේ කෙසේද?" },
        BuildSiFaq());

    private static readonly LocalePack Ta = new(
        "தகவலுக்கு மட்டும். இந்த அரட்டை எந்த கோரிக்கையையும் சமர்ப்பிக்க, அங்கீகரிக்க, நிராகரிக்க அல்லது மாற்றாது. நேரடி தங்குமிட கிடைப்புநிலை, சரக்கு அளவுகள் அல்லது செயல்பாட்டு நிலையை வழங்காது. யாராவது உடனடி ஆபத்தில் இருந்தால், உள்ளூர் அவசர சேவைகளை தொடர்புகொள்ளவும்.",
        "AI வழங்குநர் அமைக்கப்படவில்லை. SAFEWAY இன் சரிபார்க்கப்பட்ட FAQ வழிகாட்டுதலை காட்டுகிறது.",
        "AI வழங்குநர் தற்காலிகமாக கிடைக்கவில்லை. SAFEWAY இன் சரிபார்க்கப்பட்ட FAQ வழிகாட்டுதலை காட்டுகிறது.",
        "You are the SAFEWAY informational assistant. Answer only general questions about how the platform works. Never claim live emergency information, shelter availability, inventory quantities or operational status. Never submit, approve, reject or change a request, resource, shelter, volunteer assignment or decision. Never provide medical, legal or financial instructions beyond general SAFEWAY guidance. For immediate danger, tell the user to contact local emergency services. Direct operational decisions to authorised human staff. Respond in Tamil.",
        "யாராவது உடனடி ஆபத்தில் இருந்தால், முதலில் உள்ளூர் அவசர சேவைகளை தொடர்புகொள்ளவும். SAFEWAY அவசர அழைப்பு சேவை அல்ல; இது உதவியை ஒருங்கிணைக்கிறது. அனைவரும் பாதுகாப்பாக இருந்த பின், மனித மதிப்பாய்வுக்காக சம்பவத்தை புகாரளிக்கலாம்.",
        "நான் தகவல் வழிகாட்டுதலை மட்டுமே வழங்க முடியும். எந்த முடிவையும் அல்லது செயல்பாட்டு நடவடிக்கையையும் அங்கீகரிக்கப்பட்ட மனித பணியாளர்கள் மதிப்பாய்வு செய்து பதிவு செய்ய வேண்டும்.",
        "சம்பவத்தை புகாரளித்தல், உதவி தேடுதல், தங்குமிடங்கள், வளங்கள், தன்னார்வம், நன்கொடை அல்லது மனித முடிவுகள் பற்றி ஒரு கேள்வியை உள்ளிடவும்.",
        "சம்பவத்தை எப்படி புகாரளிப்பது, உதவி மற்றும் தங்குமிடங்களை கண்டுபிடிப்பது, வளங்கள் மற்றும் பதில் பணிப்பாய்வை புரிந்துகொள்வது, தன்னார்வம், நன்கொடை, மற்றும் அங்கீகரிக்கப்பட்டவர்கள் முடிவெடுக்கும் முறை ஆகியவற்றை நான் விளக்கலாம்.",
        new[] { "சம்பவத்தை எப்படி புகாரளிப்பது?", "உதவியை எப்படி கண்டுபிடிப்பது?" },
        BuildTaFaq());

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

        var pack = ResolvePack(request.Locale);
        var endpoint = _configuration["Ai:Chat:Endpoint"];
        var providerConfigured = !string.IsNullOrWhiteSpace(endpoint);
        var notice = providerConfigured ? string.Empty : pack.NotConfiguredNotice;

        var question = RuleBasedProvider.ScrubPii(request.Message?.Trim() ?? string.Empty);
        if (question.Length == 0)
        {
            return RuleAnswer(pack, pack.EmptyQuestion, Array.Empty<string>(), notice);
        }

        if (question.Length > MaxMessageLength)
        {
            question = question[..MaxMessageLength];
        }

        // Emergency-first: never let an optional provider delay local emergency guidance.
        if (IsImmediateDanger(question))
        {
            return RuleAnswer(pack, pack.EmergencyAnswer, pack.DefaultSuggestions, notice);
        }

        if (providerConfigured)
        {
            try
            {
                var answer = await AskLlmAsync(question, endpoint!, pack, cancellationToken);
                if (answer != null)
                {
                    return answer;
                }

                notice = pack.ProviderUnavailableNotice;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                notice = pack.ProviderUnavailableNotice;
            }
            catch (Exception)
            {
                notice = pack.ProviderUnavailableNotice;
            }
        }

        var match = pack.Faq
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
            ? RuleAnswer(pack, pack.DefaultAnswer, pack.DefaultSuggestions, notice)
            : RuleAnswer(pack, match.Entry.Answer, match.Entry.Suggestions, notice);
    }

    private static LocalePack ResolvePack(string? locale)
    {
        var normalized = (locale ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "si" or "si-lk" or "sinhala" => Si,
            "ta" or "ta-lk" or "tamil" => Ta,
            _ => En
        };
    }

    private async Task<AiChatResponseDto?> AskLlmAsync(
        string question,
        string endpoint,
        LocalePack pack,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CallTimeout);

        var body = JsonSerializer.Serialize(new { question, instructions = pack.LlmInstruction, locale = GetLocaleCode(pack) });
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
            Answer = NormalizeAnswer(answer, pack) + " " + pack.Disclaimer,
            Source = Cms.Domain.Constants.RecommendationSources.Ai,
            Suggestions = suggestions,
            DecidesNothing = true,
            Notice = string.Empty
        };
    }

    private static string GetLocaleCode(LocalePack pack)
    {
        if (ReferenceEquals(pack, Si)) return "si";
        if (ReferenceEquals(pack, Ta)) return "ta";
        return "en";
    }

    private static AiChatResponseDto RuleAnswer(
        LocalePack pack,
        string answer,
        IReadOnlyList<string> suggestions,
        string notice) =>
        new()
        {
            Answer = NormalizeAnswer(answer, pack) + " " + pack.Disclaimer,
            Source = Cms.Domain.Constants.RecommendationSources.Rule,
            Suggestions = suggestions,
            DecidesNothing = true,
            Notice = notice
        };

    private static bool IsImmediateDanger(string question) =>
        EmergencyKeywords.Any(keyword =>
            question.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeAnswer(string answer, LocalePack pack)
    {
        var text = Truncate(answer.Trim());
        if (string.IsNullOrWhiteSpace(text))
        {
            return pack.DefaultAnswer;
        }

        // Defensive guard: never allow provider text to imply autonomous critical actions.
        foreach (var forbidden in new[]
                 {
                     "I have approved", "I approved", "I rejected", "I dispatched",
                     "I allocated", "I submitted", "I closed the request"
                 })
        {
            if (text.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            {
                return pack.SafetyGuardAnswer;
            }
        }

        return text;
    }

    private static string Truncate(string value) =>
        value.Length <= 4000 ? value : value[..4000];

    private static IReadOnlyList<FaqEntry> BuildEnFaq() => new[]
    {
        new FaqEntry(new[] { "emergency", "ambulance", "fire", "police", "danger", "immediate", "urgent", "life threatening" },
            "If anyone is in immediate danger, contact your local emergency services first. SAFEWAY is for coordinating aid and is not an emergency call service. After everyone is safe, you can report the incident for human triage.",
            new[] { "How do I report an incident?", "How do I find assistance?" }),
        new FaqEntry(new[] { "report", "incident", "submit", "request", "start", "new" },
            "Use Report to submit a request or incident. Include the category, urgency, a short description and the location. A responder verifies it before AI classification is recorded for human review. Use the official forms for sensitive details rather than this chat.",
            new[] { "How do I check my request status?", "Who makes the final decision?" }),
        new FaqEntry(new[] { "shelter", "accommodation", "housing", "bed", "sleep" },
            "Open the Shelters page for the current verified shelter information maintained by authorised staff. This chat cannot confirm bed availability, eligibility or operational status. For urgent placement, contact the listed service centre or local emergency services.",
            new[] { "How do I find assistance?", "How do I report an incident?" }),
        new FaqEntry(new[] { "resource", "resources", "inventory", "supply", "supplies", "stock" },
            "The Resources page shows inventory, matching and dispatch information maintained by authorised logistics staff. This chat does not display live quantities and cannot allocate supplies. Ask a coordinator or logistics manager about a specific operational request.",
            new[] { "Who makes the final decision?", "How does the workflow work?" }),
        new FaqEntry(new[] { "volunteer", "volunteering", "join", "sign up", "activity", "activities" },
            "Use the Volunteers page to register a profile, record availability and see assigned activities. A volunteer coordinator verifies profiles and assigns tasks; the AI assistant cannot approve volunteer status.",
            new[] { "How do I sign up to volunteer?", "How can I donate?" }),
        new FaqEntry(new[] { "donat", "donation", "donations", "give", "fund", "funding", "money" },
            "The Donations page explains how to support SAFEWAY and how contributions are recorded by authorised staff. Do not send card numbers or bank details to this chat. For a specific donation question, contact the donations team.",
            new[] { "How do I volunteer?", "How do I find assistance?" }),
        new FaqEntry(new[] { "assistance", "help", "aid", "support", "find", "service centre", "services" },
            "Use Find assistance to see published alerts, active service centres and safety guidance. Availability and operational status are maintained by authorised staff; this chat does not create a request or confirm live capacity. If danger is immediate, contact local emergency services.",
            new[] { "How do I find a shelter?", "How do I report an incident?" }),
        new FaqEntry(new[] { "status", "track", "notification", "notifications", "inbox", "bell", "update" },
            "Request status changes create a notification. Open the notification bell or inbox to see messages delivered to your account. Signed-in users can also track submissions from My Reports.",
            new[] { "How are duplicate requests handled?", "What does Recommended mean?" }),
        new FaqEntry(new[] { "decide", "decision", "approve", "approval", "reject", "who", "final", "authorise", "authorize" },
            "AI only suggests recommendations. An authorised human records an Approved, Modified, Rejected or Deferred decision, and only that recorded decision can authorise a critical action.",
            new[] { "What does Recommended mean?", "How do I submit a request?" }),
        new FaqEntry(new[] { "duplicate", "same", "again", "merge" },
            "Similar recent requests can appear as duplicate candidates for a human to review. The system links candidates only; it never deletes or merges a request from AI output alone.",
            new[] { "Who makes the final decision?", "How do I check my request status?" }),
        new FaqEntry(new[] { "workflow", "process", "triage", "triaged", "recommended", "allocated", "dispatched", "fulfilled", "stage", "lifecycle" },
            "A request normally moves from Submitted to Triaged, then Recommended after an approved match, then Allocated, Dispatched and Fulfilled. AI can suggest at each stage, but only authorised human decisions unlock critical steps.",
            new[] { "Who makes the final decision?", "How do I check my request status?" }),
        new FaqEntry(new[] { "login", "log in", "register", "registration", "account", "password", "role" },
            "Use Login for an existing account or Register to create one. After signing in, your dashboard and navigation reflect your assigned role. Role changes are managed by authorised administrators, not by this chat.",
            new[] { "How do I submit a request?", "How do I check my request status?" }),
        new FaqEntry(new[] { "privacy", "private", "personal", "data", "secure", "security", "scrub" },
            "Do not share personal, medical or payment details in this chat. The backend scrubs personal details before an optional AI call and this assistant never changes a record. Use the official SAFEWAY forms for sensitive information.",
            new[] { "How do I submit a request?", "Who makes the final decision?" }),
        new FaqEntry(new[] { "use", "using", "safeway", "platform", "portal", "guide", "faq", "question" },
            "SAFEWAY helps you report incidents, find published assistance and coordinate a verified human-led response. Start with Report, Find assistance and Services, then use your dashboard after signing in. I answer questions only.",
            new[] { "How do I report an incident?", "How do I find assistance?" })
    };

    private static IReadOnlyList<FaqEntry> BuildSiFaq() => new[]
    {
        new FaqEntry(new[] { "හදිසි", "අනතුර", "ගින්න", "පොලිස්", "ගිලන්", "ක්ෂණික", "ජීවිත" },
            "කිසිවෙකු ක්ෂණික අනතුරක සිටී නම්, පළමුව දේශීය හදිසි සේවාවන් අමතන්න. SAFEWAY හදිසි ඇමතුම් සේවාවක් නොවේ; එය සහාය සම්බන්ධීකරණය සඳහාය. සියල්ලන් ආරක්ෂිත වූ පසු, මානුෂීය සමාලෝචනය සඳහා සිදුවීම වාර්තා කළ හැක.",
            new[] { "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?", "සහාය සොයාගන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "වාර්තා", "සිදුවීම", "ඉල්ලීම", "යොමු", "ඉදිරිපත්" },
            "ඉල්ලීමක් හෝ සිදුවීමක් ඉදිරිපත් කිරීමට වාර්තා කිරීම භාවිත කරන්න. ප්‍රවර්ගය, හදිසි බව, කෙටි විස්තරයක් සහ ස්ථානය ඇතුළත් කරන්න. මානුෂීය සමාලෝචනයට පෙර ප්‍රතිචාරකයෙකු එය සත්‍යාපනය කරයි. සංවේදී තොරතුරු සඳහා මෙම කතාබහ වෙනුවට නිල පෝරම භාවිත කරන්න.",
            new[] { "මගේ ඉල්ලීමේ තත්ත්වය පරීක්ෂා කරන්නේ කෙසේද?", "අවසන් තීරණය ගන්නේ කවුද?" }),
        new FaqEntry(new[] { "ආරක්ෂිත", "නවාතැන්", "තැන", "ඇඳ" },
            "බලයලත් කාර්ය මණ්ඩලය නඩත්තු කරන සත්‍යාපිත ආරක්ෂිත ස්ථාන තොරතුරු සඳහා ආරක්ෂිත ස්ථාන පිටුව විවෘත කරන්න. මෙම කතාබහට ඇඳ ලබාගත හැකි බව, සුදුසුකම් හෝ මෙහෙයුම් තත්ත්වය තහවුරු කළ නොහැක. හදිසි අවශ්‍යතාවක් නම්, ලැයිස්තුගත සේවා මධ්‍යස්ථානය හෝ දේශීය හදිසි සේවාවන් අමතන්න.",
            new[] { "සහාය සොයාගන්නේ කෙසේද?", "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "සම්පත්", "ඉන්වෙන්ටරි", "සැපයුම්", "තොග" },
            "සම්පත් පිටුව බලයලත් ලොජිස්ටික්ස් කාර්ය මණ්ඩලය නඩත්තු කරන ඉන්වෙන්ටරි, ගැලපීම සහ යැවීම් තොරතුරු පෙන්වයි. මෙම කතාබහ සජීවී ප්‍රමාණ පෙන්වන්නේ නැත සහ සම්පත් වෙන් කළ නොහැක. නිශ්චිත මෙහෙයුම් ඉල්ලීමක් ගැන සම්බන්ධීකාරකයෙකුගෙන් විමසන්න.",
            new[] { "අවසන් තීරණය ගන්නේ කවුද?", "ක්‍රියාවලිය ක්‍රියා කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "ස්වේච්ඡා", "ස්වෙච්ඡා", "සම්බන්ධ", "එක්වන්න" },
            "පැතිකඩක් ලියාපදිංචි කිරීමට, ලබාගත හැකි කාලය සටහන් කිරීමට සහ පවරා ඇති ක්‍රියාකාරකම් බැලීමට ස්වේච්ඡා සේවකයින් පිටුව භාවිත කරන්න. ස්වේච්ඡා සම්බන්ධීකාරකයෙකු පැතිකඩ සත්‍යාපනය කර කාර්යයන් පවරයි; AI සහායකයට ස්වේච්ඡා තත්ත්වය අනුමත කළ නොහැක.",
            new[] { "ස්වේච්ඡාවෙන් සම්බන්ධ වන්නේ කෙසේද?", "පරිත්‍යාග කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "පරිත්‍යාග", "दान", "මුදල්", "ආධාර" },
            "පරිත්‍යාග පිටුව SAFEWAY ට සහාය වන ආකාරය සහ බලයලත් කාර්ය මණ්ඩලය දායකත්ව සටහන් කරන ආකාරය පැහැදිලි කරයි. කාඩ් අංක හෝ බැංකු තොරතුරු මෙම කතාබහට නොයවන්න. නිශ්චිත පරිත්‍යාග ප්‍රශ්නයක් සඳහා පරිත්‍යාග කණ්ඩායම අමතන්න.",
            new[] { "ස්වේච්ඡාවෙන් සම්බන්ධ වන්නේ කෙසේද?", "සහාය සොයාගන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "සහාය", "උදව්", "සොයන්න", "සේවා" },
            "ප්‍රකාශිත අනතුරු ඇඟවීම්, ක්‍රියාකාරී සේවා මධ්‍යස්ථාන සහ ආරක්ෂණ මාර්ගෝපදේශ බැලීමට සහාය සොයන්න භාවිත කරන්න. ලබාගත හැකි බව සහ මෙහෙයුම් තත්ත්වය බලයලත් කාර්ය මණ්ඩලය විසින් නඩත්තු කරයි; මෙම කතාබහ ඉල්ලීමක් සාදන්නේ නැත හෝ සජීවී ධාරිතාව තහවුරු නොකරයි.",
            new[] { "ආරක්ෂිත ස්ථානයක් සොයාගන්නේ කෙසේද?", "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "තත්ත්වය", "නිරීක්ෂණ", "දැනුම්දීම", "යාවත්කාල" },
            "ඉල්ලීමේ තත්ත්ව වෙනස්වීම් දැනුම්දීමක් සාදයි. ඔබගේ ගිණුමට ලබාදුන් පණිවිඩ බැලීමට දැනුම්දීම් සීනුව හෝ එන ලිපි විවෘත කරන්න. පිවිසි පරිශීලකයින්ට මගේ වාර්තා හරහා ඉදිරිපත් කිරීම් නිරීක්ෂණය කළ හැක.",
            new[] { "අනුපිටපත් ඉල්ලීම් හසුරුවන්නේ කෙසේද?", "නිර්දේශිත යන්නෙන් අදහස් කරන්නේ කුමක්ද?" }),
        new FaqEntry(new[] { "තීරණ", "අනුමත", "ප්‍රතික්ෂේප", "කවුද", "අවසන්" },
            "AI යෝජනා පමණක් ලබාදේ. බලයලත් මානුෂීය පුද්ගලයෙකු අනුමත, වෙනස් කළ, ප්‍රතික්ෂේප කළ හෝ කල් දැමූ තීරණයක් වාර්තා කරයි. එම වාර්තාගත තීරණය පමණක් තීරණාත්මක ක්‍රියාවක් අනුමත කළ හැක.",
            new[] { "නිර්දේශිත යන්නෙන් අදහස් කරන්නේ කුමක්ද?", "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "ක්‍රියාවලිය", "කාර්යප්‍රවාහ", "පියවර" },
            "ඉල්ලීමක් සාමාන්‍යයෙන් ඉදිරිපත් කළ -> ත්‍රියාජ් කළ -> නිර්දේශිත -> වෙන් කළ -> යවන ලද -> සම්පූර්ණ කළ ලෙස ගමන් කරයි. AI සෑම අදියරකදීම යෝජනා කළ හැක, නමුත් බලයලත් මානුෂීය තීරණ පමණක් තීරණාත්මක පියවර අගුළු හරියි.",
            new[] { "අවසන් තීරණය ගන්නේ කවුද?", "මගේ ඉල්ලීමේ තත්ත්වය පරීක්ෂා කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "පිවිස", "ලියාපදිංචි", "ගිණුම", "මුරපද" },
            "පවතින ගිණුමක් සඳහා පිවිසෙන්න භාවිත කරන්න හෝ නව ගිණුමක් සඳහා ලියාපදිංචි වන්න. පිවිසීමෙන් පසු ඔබගේ උපකරණ පුවරුව සහ සංචාලනය ඔබට පවරා ඇති භූමිකාව පිළිබිඹු කරයි. භූමිකා වෙනස් කිරීම් බලයලත් පරිපාලකයින් විසින් කළමනාකරණය කරයි.",
            new[] { "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?", "මගේ ඉල්ලීමේ තත්ත්වය පරීක්ෂා කරන්නේ කෙසේද?" }),
        new FaqEntry(new[] { "රහස්‍ය", "පුද්ගලික", "දත්ත", "ආරක්ෂිත" },
            "මෙම කතාබහ තුළ පුද්ගලික, වෛද්‍ය හෝ ගෙවීම් තොරතුරු බෙදා නොගන්න. විකල්ප AI ඇමතුමකට පෙර පසුබිම පුද්ගලික තොරතුරු ඉවත් කරයි. සංවේදී තොරතුරු සඳහා නිල SAFEWAY පෝරම භාවිත කරන්න.",
            new[] { "සිදුවීමක් වාර්තා කරන්නේ කෙසේද?", "අවසන් තීරණය ගන්නේ කවුද?" })
    };

    private static IReadOnlyList<FaqEntry> BuildTaFaq() => new[]
    {
        new FaqEntry(new[] { "அவசர", "ஆபத்து", "தீ", "காவல்", "ஆம்புலன்ஸ்", "உடனடி", "உயிர்" },
            "யாராவது உடனடி ஆபத்தில் இருந்தால், முதலில் உள்ளூர் அவசர சேவைகளை தொடர்புகொள்ளவும். SAFEWAY அவசர அழைப்பு சேவை அல்ல; இது உதவியை ஒருங்கிணைக்கிறது. அனைவரும் பாதுகாப்பாக இருந்த பின், மனித மதிப்பாய்வுக்காக சம்பவத்தை புகாரளிக்கலாம்.",
            new[] { "சம்பவத்தை எப்படி புகாரளிப்பது?", "உதவியை எப்படி கண்டுபிடிப்பது?" }),
        new FaqEntry(new[] { "புகார்", "சம்பவம்", "சமர்ப்பி", "கோரிக்கை", "புதிய" },
            "கோரிக்கை அல்லது சம்பவத்தை சமர்ப்பிக்க புகாரளி என்பதை பயன்படுத்தவும். வகை, அவசரம், குறுகிய விளக்கம் மற்றும் இடத்தை சேர்க்கவும். மனித மதிப்பாய்வுக்கு முன் பதிலளிப்பவர் அதை சரிபார்ப்பார். உணர்திறன் விவரங்களுக்கு இந்த அரட்டைக்கு பதிலாக அதிகாரப்பூர்வ படிவங்களை பயன்படுத்தவும்.",
            new[] { "என் கோரிக்கை நிலையைக் எப்படி சரிபார்ப்பது?", "இறுதி முடிவை யார் எடுப்பது?" }),
        new FaqEntry(new[] { "தங்குமிடம்", "தங்குமிட", "படுக்கை", "இருப்பு" },
            "அங்கீகரிக்கப்பட்ட பணியாளர்கள் பராமரிக்கும் சரிபார்க்கப்பட்ட தங்குமிட தகவலுக்கு தங்குமிடங்கள் பக்கத்தை திறக்கவும். இந்த அரட்டை படுக்கை கிடைப்புநிலை, தகுதி அல்லது செயல்பாட்டு நிலையை உறுதிப்படுத்த முடியாது. அவசர தேவைக்கு பட்டியலிடப்பட்ட சேவை மையம் அல்லது உள்ளூர் அவசர சேவைகளை தொடர்புகொள்ளவும்.",
            new[] { "உதவியை எப்படி கண்டுபிடிப்பது?", "சம்பவத்தை எப்படி புகாரளிப்பது?" }),
        new FaqEntry(new[] { "வளம்", "வளங்கள்", "சரக்கு", "பொருட்கள்", "இருப்பு" },
            "வளங்கள் பக்கம் அங்கீகரிக்கப்பட்ட தளவாட பணியாளர்கள் பராமரிக்கும் சரக்கு, பொருத்தம் மற்றும் அனுப்புதல் தகவலை காட்டுகிறது. இந்த அரட்டை நேரடி அளவுகளை காட்டாது மற்றும் பொருட்களை ஒதுக்க முடியாது. குறிப்பிட்ட செயல்பாட்டு கோரிக்கைக்கு ஒருங்கிணைப்பாளரை கேளுங்கள்.",
            new[] { "இறுதி முடிவை யார் எடுப்பது?", "பணிப்பாய்வு எப்படி செயல்படுகிறது?" }),
        new FaqEntry(new[] { "தன்னார்வ", "தன்னார்வலர்", "சேர", "பதிவு" },
            "சுயவிவரத்தை பதிவு செய்ய, கிடைக்கும் நேரத்தை பதிவு செய்ய மற்றும் ஒதுக்கப்பட்ட செயல்பாடுகளை காண தன்னார்வலர்கள் பக்கத்தை பயன்படுத்தவும். தன்னார்வ ஒருங்கிணைப்பாளர் சுயவிவரங்களை சரிபார்த்து பணிகளை ஒதுக்குகிறார்; AI உதவியாளர் தன்னார்வ நிலையை அங்கீகரிக்க முடியாது.",
            new[] { "தன்னார்வலராக எப்படி சேர்வது?", "நன்கொடை எப்படி வழங்குவது?" }),
        new FaqEntry(new[] { "நன்கொடை", "நிதி", "பணம்", "உதவு" },
            "நன்கொடைகள் பக்கம் SAFEWAY-க்கு எப்படி ஆதரவளிப்பது மற்றும் அங்கீகரிக்கப்பட்ட பணியாளர்கள் பங்களிப்புகளை எப்படி பதிவு செய்கிறார்கள் என்பதை விளக்குகிறது. அட்டை எண்கள் அல்லது வங்கி விவரங்களை இந்த அரட்டையில் அனுப்ப வேண்டாம். குறிப்பிட்ட நன்கொடை கேள்விக்கு நன்கொடை குழுவை தொடர்புகொள்ளவும்.",
            new[] { "தன்னார்வலராக எப்படி சேர்வது?", "உதவியை எப்படி கண்டுபிடிப்பது?" }),
        new FaqEntry(new[] { "உதவி", "தேடு", "சேவை", "ஆதரவு" },
            "வெளியிடப்பட்ட எச்சரிக்கைகள், செயலில் உள்ள சேவை மையங்கள் மற்றும் பாதுகாப்பு வழிகாட்டுதலை காண உதவியைத் தேடவும் பயன்படுத்தவும். கிடைப்புநிலை மற்றும் செயல்பாட்டு நிலை அங்கீகரிக்கப்பட்ட பணியாளர்களால் பராமரிக்கப்படுகிறது; இந்த அரட்டை கோரிக்கையை உருவாக்காது அல்லது நேரடி திறனை உறுதிப்படுத்தாது.",
            new[] { "தங்குமிடத்தை எப்படி கண்டுபிடிப்பது?", "சம்பவத்தை எப்படி புகாரளிப்பது?" }),
        new FaqEntry(new[] { "நிலை", "கண்காணி", "அறிவிப்பு", "புதுப்பிப்பு" },
            "கோரிக்கை நிலை மாற்றங்கள் அறிவிப்பை உருவாக்கும். உங்கள் கணக்கிற்கு வழங்கப்பட்ட செய்திகளை காண அறிவிப்பு மணி அல்லது உள்பெட்டியை திறக்கவும். உள்நுழைந்த பயனர்கள் என் புகார்கள் மூலம் சமர்ப்பிப்புகளை கண்காணிக்கலாம்.",
            new[] { "நகல் கோரிக்கைகள் எப்படி கையாளப்படுகின்றன?", "பரிந்துரைக்கப்பட்டது என்றால் என்ன?" }),
        new FaqEntry(new[] { "முடிவு", "அங்கீகரி", "நிராகரி", "யார்", "இறுதி" },
            "AI பரிந்துரைகளை மட்டுமே வழங்குகிறது. அங்கீகரிக்கப்பட்ட மனிதர் அங்கீகரிக்கப்பட்டது, மாற்றப்பட்டது, நிராகரிக்கப்பட்டது அல்லது ஒத்திவைக்கப்பட்டது என்ற முடிவை பதிவு செய்கிறார். அந்த பதிவு செய்யப்பட்ட முடிவு மட்டுமே முக்கியமான நடவடிக்கையை அங்கீகரிக்க முடியும்.",
            new[] { "பரிந்துரைக்கப்பட்டது என்றால் என்ன?", "சம்பவத்தை எப்படி புகாரளிப்பது?" }),
        new FaqEntry(new[] { "பணிப்பாய்வு", "செயல்முறை", "நிலை" },
            "ஒரு கோரிக்கை பொதுவாக சமர்ப்பிக்கப்பட்டது -> மதிப்பாய்வு செய்யப்பட்டது -> பரிந்துரைக்கப்பட்டது -> ஒதுக்கப்பட்டது -> அனுப்பப்பட்டது -> நிறைவு பெற்றது என நகரும். AI ஒவ்வொரு கட்டத்திலும் பரிந்துரைக்கலாம், ஆனால் அங்கீகரிக்கப்பட்ட மனித முடிவுகள் மட்டுமே முக்கியமான படிகளை திறக்கும்.",
            new[] { "இறுதி முடிவை யார் எடுப்பது?", "என் கோரிக்கை நிலையைக் எப்படி சரிபார்ப்பது?" }),
        new FaqEntry(new[] { "உள்நுழை", "பதிவு", "கணக்கு", "கடவுச்சொல்" },
            "இருக்கும் கணக்கிற்கு உள்நுழை என்பதை பயன்படுத்தவும் அல்லது புதிய கணக்கிற்கு பதிவு செய்யவும். உள்நுழைந்த பிறகு உங்கள் கட்டுப்பாட்டு பலகை மற்றும் வழிசெலுத்தல் உங்கள் ஒதுக்கப்பட்ட பங்கை பிரதிபலிக்கும். பங்கு மாற்றங்களை அங்கீகரிக்கப்பட்ட நிர்வாகிகள் நிர்வகிக்கிறார்கள்.",
            new[] { "சம்பவத்தை எப்படி புகாரளிப்பது?", "என் கோரிக்கை நிலையைக் எப்படி சரிபார்ப்பது?" }),
        new FaqEntry(new[] { "தனியுரிமை", "தனிப்பட்ட", "தரவு", "பாதுகாப்பு" },
            "இந்த அரட்டையில் தனிப்பட்ட, மருத்துவ அல்லது கட்டண விவரங்களை பகிர வேண்டாம். விருப்ப AI அழைப்புக்கு முன் பின்புலம் தனிப்பட்ட விவரங்களை அகற்றும். உணர்திறன் தகவலுக்கு அதிகாரப்பூர்வ SAFEWAY படிவங்களை பயன்படுத்தவும்.",
            new[] { "சம்பவத்தை எப்படி புகாரளிப்பது?", "இறுதி முடிவை யார் எடுப்பது?" })
    };
}
