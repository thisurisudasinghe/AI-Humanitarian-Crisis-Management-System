using Cms.Application.Contracts.Operations;
using Cms.Domain.Entities.Operations;

namespace Cms.Infrastructure.Services.Operations;

/// <summary>
/// Deterministic, explainable resource-match scoring. It never mutates stock and
/// never authorises an allocation on its own; a recorded human decision is still
/// required for every critical action.
/// </summary>
public sealed class WeightedResourceMatchScoringStrategy : IResourceMatchScoringStrategy
{
    private const decimal AvailabilityWeight = 0.6m;
    private const decimal ReorderWeight = 0.2m;
    private const decimal ExpiryWeight = 0.2m;

    public (decimal Score, string Rationale) Score(Inventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var availability = inventory.Quantity <= 0
            ? 0m
            : Clamp(inventory.Available / inventory.Quantity);

        var reorderHeadroom = inventory.ReorderThreshold <= 0
            ? 1m
            : Clamp(inventory.Available / (inventory.ReorderThreshold * 2m));

        var expiryConfidence = 1m;
        if (inventory.ExpiryDate is { } expiry)
        {
            var days = (expiry - DateTime.UtcNow).TotalDays;
            expiryConfidence = Clamp((decimal)Math.Max(0, days) / 30m);
        }

        var score = Clamp(
            availability * AvailabilityWeight +
            reorderHeadroom * ReorderWeight +
            expiryConfidence * ExpiryWeight);

        var rationale =
            $"Available stock {availability:P0} contributes 60%; " +
            $"reorder headroom {reorderHeadroom:P0} contributes 20%; " +
            $"expiry confidence {expiryConfidence:P0} contributes 20%.";

        return (score, rationale);
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);
}
