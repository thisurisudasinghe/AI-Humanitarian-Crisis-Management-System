using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Operations;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// Deterministic offline provider. Requires no API key and never sends personal data anywhere.
/// </summary>
public sealed class RuleBasedProvider : IAiProvider
{
    private static readonly IReadOnlyDictionary<Category, string[]> CategoryKeywords =
        new Dictionary<Category, string[]>
        {
            [Category.Food] = new[] { "food", "water", "meal", "hunger", "starving", "rations" },
            [Category.Medicine] = new[] { "medicine", "medical", "injured", "wound", "doctor", "hospital", "medication" },
            [Category.Shelter] = new[] { "shelter", "housing", "homeless", "tent", "sleep", "evacuated" },
            [Category.Transport] = new[] { "transport", "vehicle", "evacuation", "bus", "car", "ambulance" },
            [Category.Accessibility] = new[] { "wheelchair", "accessible", "disability", "blind", "deaf", "mobility" },
            [Category.Other] = Array.Empty<string>()
        };

    private static readonly string[] CriticalKeywords =
        { "critical", "unconscious", "bleeding", "trapped", "life", "fire", "flood", "collapsed" };

    private static readonly string[] HighKeywords =
        { "urgent", "injured", "elderly", "child", "pregnant", "no shelter", "without water" };

    private static readonly Regex EmailPattern =
        new(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhonePattern =
        new(@"\+?\d[\d\s().-]{7,}\d", RegexOptions.Compiled);

    private static readonly Regex DigitPattern = new(@"\d+", RegexOptions.Compiled);

    private static readonly Regex TokenPattern = new(@"[a-z]+", RegexOptions.Compiled);

    public string Name => "RuleBasedProvider";

    public Task<AiAnalysisResult> AnalyzeAsync(
        Request request,
        IReadOnlyList<Request> candidateRequests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var text = ScrubPii($"{request.Category} {request.Urgency} {request.Description}");
        var suggestions = new List<AiSuggestionDto>();

        var detectedCategory = DetectCategory(text, request.Category, out var categoryHits);
        suggestions.Add(new AiSuggestionDto
        {
            Type = RecommendationTypes.Classification,
            TargetId = request.Id,
            PayloadJson = JsonSerializer.Serialize(new { category = detectedCategory.ToString() }),
            Confidence = Confidence(categoryHits),
            Rationale = categoryHits == 0
                ? "No category keywords found; retained the submitted category."
                : $"Matched {categoryHits} category keyword(s) for {detectedCategory}.",
            Source = RecommendationSources.Rule
        });

        var detectedUrgency = DetectUrgency(text, request.Urgency, out var urgencyHits);
        suggestions.Add(new AiSuggestionDto
        {
            Type = RecommendationTypes.Priority,
            TargetId = request.Id,
            PayloadJson = JsonSerializer.Serialize(new
            {
                urgency = detectedUrgency.ToString(),
                priorityScore = PriorityScore(detectedUrgency)
            }),
            Confidence = Confidence(urgencyHits),
            Rationale = urgencyHits == 0
                ? "No urgency keywords found; retained the submitted urgency."
                : $"Matched {urgencyHits} urgency signal(s) for {detectedUrgency}.",
            Source = RecommendationSources.Rule
        });

        var duplicate = FindDuplicate(request, text, candidateRequests);
        if (duplicate != null)
        {
            suggestions.Add(duplicate);
        }

        // Anomaly flag: the submitted urgency materially understates the content.
        // This is a recommendation only — it never changes the request itself.
        if (detectedUrgency == Urgency.Critical &&
            request.Urgency is Urgency.Low or Urgency.Medium)
        {
            suggestions.Add(new AiSuggestionDto
            {
                Type = RecommendationTypes.Anomaly,
                TargetId = request.Id,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    kind = "UnderstatedUrgency",
                    submitted = request.Urgency.ToString(),
                    suggested = detectedUrgency.ToString()
                }),
                Confidence = 0.8m,
                Rationale = "Text contains critical signals while the submitted urgency is low or medium.",
                Source = RecommendationSources.Rule
            });
        }

        return Task.FromResult(new AiAnalysisResult
        {
            Source = RecommendationSources.Rule,
            Suggestions = suggestions
        });
    }

    private static AiSuggestionDto? FindDuplicate(
        Request request,
        string text,
        IReadOnlyList<Request> candidates)
    {
        var tokens = Tokenize(text);
        if (tokens.Count == 0)
        {
            return null;
        }

        var cutoff = DateTime.UtcNow.AddHours(-24);
        AiSuggestionDto? best = null;
        var bestScore = 0.0;

        foreach (var candidate in candidates)
        {
            if (candidate.Id == request.Id ||
                candidate.Category != request.Category ||
                candidate.SubmittedAt < cutoff ||
                GeoDistance.HaversineKm(
                    (double)request.Lat,
                    (double)request.Lng,
                    (double)candidate.Lat,
                    (double)candidate.Lng) > 0.5)
            {
                continue;
            }

            var candidateTokens = Tokenize(ScrubPii(
                $"{candidate.Category} {candidate.Urgency} {candidate.Description}"));
            if (candidateTokens.Count == 0)
            {
                continue;
            }

            var intersection = tokens.Intersect(candidateTokens).Count();
            var union = tokens.Union(candidateTokens).Count();
            var score = union == 0 ? 0 : (double)intersection / union;

            if (score > bestScore)
            {
                bestScore = score;
                best = new AiSuggestionDto
                {
                    Type = RecommendationTypes.Duplicate,
                    TargetId = request.Id,
                    PayloadJson = JsonSerializer.Serialize(
                        new { duplicateTargetId = candidate.Id, similarity = Math.Round(score, 3) }),
                    Confidence = (decimal)Math.Round(score, 3),
                    Rationale =
                        $"Same category, within 500 m, submitted in the last 24 h, " +
                        $"with {score:P0} text similarity. Candidate only; never merged automatically.",
                    Source = RecommendationSources.Rule
                };
            }
        }

        return bestScore >= 0.6 ? best : null;
    }

    private static Category DetectCategory(string text, Category fallback, out int hits)
    {
        hits = 0;
        var best = fallback;
        var bestCount = 0;

        foreach (var (category, keywords) in CategoryKeywords)
        {
            var count = keywords.Count(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (count > bestCount)
            {
                best = category;
                bestCount = count;
            }
        }

        hits = bestCount;
        return best;
    }

    private static Urgency DetectUrgency(string text, Urgency fallback, out int hits)
    {
        var critical = CriticalKeywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (critical > 0)
        {
            hits = critical;
            return Urgency.Critical;
        }

        var high = HighKeywords.Count(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (high > 0)
        {
            hits = high;
            return Urgency.High;
        }

        hits = 0;
        return fallback;
    }

    private static decimal PriorityScore(Urgency urgency) => urgency switch
    {
        Urgency.Critical => 1.0m,
        Urgency.High => 0.8m,
        Urgency.Medium => 0.55m,
        _ => 0.3m
    };

    private static decimal Confidence(int signalCount) =>
        Math.Min(0.95m, 0.5m + signalCount * 0.1m);

    private static HashSet<string> Tokenize(string text) =>
        TokenPattern.Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 2)
            .ToHashSet();

    public static string ScrubPii(string text)
    {
        text = EmailPattern.Replace(text, " ");
        text = PhonePattern.Replace(text, " ");
        text = DigitPattern.Replace(text, " ");
        return text;
    }
}
