using Cms.Application.DTOs.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Services.AI;

namespace Cms.Tests;

public class RuleBasedProviderTests
{
    [Fact]
    public async Task AnalyzeAsync_WorksOffline_AndOnlyProducesFrozenValues()
    {
        var provider = new RuleBasedProvider();
        var request = new Request
        {
            Id = Guid.NewGuid(),
            Title = "Need food and water",
            Description = "Urgent, no water since yesterday",
            Category = Category.Other,
            Urgency = Urgency.Low
        };

        var result = await provider.AnalyzeAsync(request, Array.Empty<Request>());

        Assert.Equal(RecommendationSources.Rule, result.Source);
        Assert.True(result.Suggestions.Count >= 2);
        Assert.All(result.Suggestions, s =>
        {
            Assert.Contains(s.Type, RecommendationTypes.All);
            Assert.Equal(RecommendationSources.Rule, s.Source);
            Assert.InRange(s.Confidence, 0m, 1m);
        });
    }

    [Fact]
    public void ScrubPii_RemovesEmailsPhoneNumbersAndDigits()
    {
        var text = "Contact jane.doe@example.com or +44 7700 900123. House 42,床 7.";

        var scrubbed = RuleBasedProvider.ScrubPii(text);

        Assert.DoesNotContain("jane.doe@example.com", scrubbed);
        Assert.DoesNotContain("7700", scrubbed);
        Assert.DoesNotContain("42", scrubbed);
    }

    [Fact]
    public async Task AnalyzeAsync_FlagsUnderstatedUrgencyAsAnomaly()
    {
        var provider = new RuleBasedProvider();
        var request = new Request
        {
            Id = Guid.NewGuid(),
            Title = "Trapped and bleeding after collapse",
            Description = "Critical emergency, unconscious person",
            Category = Category.Shelter,
            Urgency = Urgency.Low
        };

        var result = await provider.AnalyzeAsync(request, Array.Empty<Request>());

        Assert.Contains(result.Suggestions, s => s.Type == RecommendationTypes.Anomaly);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsDuplicateFromNonIdentifyingTokens()
    {
        var provider = new RuleBasedProvider();
        var request = new Request
        {
            Id = Guid.NewGuid(),
            Title = "Need medicine for injured people",
            Description = "Doctor requested urgent medical help",
            Category = Category.Medicine,
            Urgency = Urgency.High
        };
        var duplicateCandidate = new Request
        {
            Id = Guid.NewGuid(),
            Title = "Need medicine for injured people",
            Description = "Doctor requested urgent medical help",
            Category = Category.Medicine,
            Urgency = Urgency.High
        };

        var result = await provider.AnalyzeAsync(request, new[] { duplicateCandidate });

        Assert.Contains(
            result.Suggestions,
            s => s.Type == RecommendationTypes.Duplicate);
    }
}
