using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Application.Contracts;
using Cms.Application.Contracts.Operations;
using Cms.Application.Interfaces;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using GeoPoint = Cms.Application.Contracts.GeoPoint;
using IGeoService = Cms.Application.Contracts.IGeoService;
using IResourceMatcher = Cms.Application.Contracts.Operations.IResourceMatcher;
using IShelterRecommender = Cms.Application.Contracts.Operations.IShelterRecommender;
using IVolunteerMatcher = Cms.Application.Contracts.Operations.IVolunteerMatcher;
using MatchCandidate = Cms.Application.Contracts.Operations.MatchCandidate;
using static Cms.Infrastructure.Operations.OperationsMatcherHelpers;

namespace Cms.Infrastructure.Operations;

/// <summary>
/// Read-only deterministic matchers. They rank candidates, never reserve or
/// allocate, and express every score on the shared 0..1 scale. Weights are
/// documented in one place: OperationsService's class comment.
/// </summary>
public sealed class ResourceMatcher(CmsDbContext db, IGeoService geo, IRequestReader requests, ICurrentUser user) : IResourceMatcher
{
    public async Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await requests.GetAsync(requestId, ct);
        if (request is null) return [];
        var requested = EstimateQuantity(request);
        var inventories = await (from inventory in db.Inventories.AsNoTracking()
                                 join type in db.ResourceTypes.AsNoTracking() on inventory.ResourceTypeId equals type.Id
                                 where inventory.Available > 0 &&
                                       (inventory.ExpiryDate == null || inventory.ExpiryDate > DateTime.UtcNow)
                                 select new { inventory, type.Name }).ToListAsync(ct);

        var output = new List<MatchCandidate>();
        foreach (var row in inventories)
        {
            var typeFit = MatchesCategory(row.Name, request.Category) ? 1m : 0.25m;
            var quantityFit = Clamp(requested <= 0 ? 1m : row.inventory.Available / requested);
            var distance = await OperationsMatcherHelpers.DistanceAsync(geo, request.Lat, request.Lng, row.inventory.Lat, row.inventory.Lng, ct);
            var distanceFit = distance is null ? 0.5m : Clamp(1m - (decimal)(distance.Km / 100d));
            var urgencyFit = (decimal)request.Urgency / (decimal)Urgency.Critical;
            var expiryFit = ExpiryFit(row.inventory.ExpiryDate);
            var accessFit = !HasAccessNeed(request) || row.inventory.AccessibilityCapable ? 1m : 0.2m;
            var orgFit = user.OrgId is null || row.inventory.OrgId == user.OrgId ? 1m : 0.5m;

            var score = Clamp(typeFit * 0.25m + quantityFit * 0.20m + distanceFit * 0.15m +
                              urgencyFit * 0.15m + expiryFit * 0.10m + accessFit * 0.10m + orgFit * 0.05m);
            var partial = row.inventory.Available < requested
                ? $" partial: {row.inventory.Available:0.##} of {requested:0.##}"
                : string.Empty;
            var approximate = distance?.IsApproximate ?? true;
            var distanceText = distance is null ? "location unavailable" : $"{(approximate ? "approx. " : string.Empty)}{distance.Km:0.##} km";
            output.Add(new MatchCandidate(
                row.inventory.Id,
                Math.Round(score, 4),
                $"Type fit {typeFit:0.##}; {row.inventory.Available:0.##} available{partial}; {distanceText}; urgency {request.Urgency}; expiry {expiryFit:0.##}; accessibility {accessFit:0.##}; org fit {orgFit:0.##}.",
                approximate));
        }
        return output.OrderByDescending(x => x.Score).ThenBy(x => x.TargetId).ToArray();
    }
}

public sealed class ShelterRecommender(CmsDbContext db, IGeoService geo, IRequestReader requests, ICurrentUser user) : IShelterRecommender
{
    public async Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await requests.GetAsync(requestId, ct);
        if (request is null) return [];
        var shelters = await db.Shelters.AsNoTracking()
            .Where(x => x.Approved && x.Capacity - x.Occupancy >= request.HouseholdSize)
            .ToListAsync(ct);

        var output = new List<MatchCandidate>();
        foreach (var shelter in shelters)
        {
            var free = shelter.Capacity - shelter.Occupancy;
            var availability = Clamp((decimal)free / Math.Max(1, request.HouseholdSize * 2));
            var accessNeed = NeedText(request, "wheelchair", "accessible", "access", "step", "lift");
            var offered = shelter.AccessibilityFeatures + " " + shelter.Facilities + " " + shelter.Services;
            var suitability = string.IsNullOrWhiteSpace(accessNeed) || MentionsAny(offered, accessNeed) ? 1m : 0.3m;
            var distance = await OperationsMatcherHelpers.DistanceAsync(geo, request.Lat, request.Lng, shelter.Lat, shelter.Lng, ct);
            var distanceFit = distance is null ? 0.5m : Clamp(1m - (decimal)(distance.Km / 100d));
            var services = string.IsNullOrWhiteSpace(accessNeed) || MentionsAny(shelter.Services, accessNeed) ? 1m : 0.5m;
            var orgFit = user.OrgId is null || shelter.OrgId == user.OrgId ? 1m : 0.5m;
            var score = Clamp(availability * 0.30m + suitability * 0.25m + distanceFit * 0.25m + services * 0.10m + orgFit * 0.10m);
            var approximate = distance?.IsApproximate ?? true;
            var distanceText = distance is null ? "location unavailable" : $"{(approximate ? "approximate" : "route")} {distance.Km:0.##} km";
            output.Add(new MatchCandidate(
                shelter.Id,
                Math.Round(score, 4),
                $"Approved shelter; {free} places free for a household of {request.HouseholdSize}; {distanceText}; suitability {suitability:0.##}; services {services:0.##}.",
                approximate));
        }
        return output.OrderByDescending(x => x.Score).ThenBy(x => x.TargetId).ToArray();
    }
}

public sealed class VolunteerMatcher(CmsDbContext db, IGeoService geo, IRequestReader requests) : IVolunteerMatcher
{
    public async Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await requests.GetAsync(requestId, ct);
        if (request is null) return [];
        var now = DateTime.UtcNow;
        var volunteers = await db.Volunteers.AsNoTracking()
            .Where(x => x.Active && x.VerificationStatus == "Verified" &&
                        db.VolunteerAvailabilities.Any(a => a.VolunteerId == x.Id && a.StartsAt <= now && a.EndsAt >= now))
            .ToListAsync(ct);
        var needed = NeededSkills(request);

        var output = new List<MatchCandidate>();
        foreach (var volunteer in volunteers)
        {
            var skills = ParseArray(volunteer.SkillsJson);
            var matched = needed.Count == 0 ? skills.Take(3).ToArray() : skills.Where(needed.Contains).ToArray();
            var skillFit = needed.Count == 0 ? (skills.Length == 0 ? 0m : 0.7m) : Clamp((decimal)matched.Length / needed.Count);
            var windows = await db.VolunteerAvailabilities.AsNoTracking()
                .Where(x => x.VolunteerId == volunteer.Id && x.EndsAt >= now).ToListAsync(ct);
            var windowFit = windows.Count == 0 ? 0m : Clamp(1m / windows.Count * 2m);
            var distance = await OperationsMatcherHelpers.DistanceAsync(geo, request.Lat, request.Lng, volunteer.Lat, volunteer.Lng, ct);
            var distanceFit = distance is null ? 0.5m : Clamp(1m - (decimal)(distance.Km / 100d));
            var contextFit = needed.Count == 0 ? 0.5m : Clamp((decimal)matched.Length / needed.Count);
            var score = Clamp(skillFit * 0.45m + windowFit * 0.25m + distanceFit * 0.20m + contextFit * 0.10m);
            var approximate = distance?.IsApproximate ?? true;
            var distanceText = distance is null ? "location unavailable" : $"{(approximate ? "approx. " : string.Empty)}{distance.Km:0.##} km";
            output.Add(new MatchCandidate(
                volunteer.Id,
                Math.Round(score, 4),
                $"Verified and available; matched skills: {(matched.Length == 0 ? "none" : string.Join(", ", matched))}; {distanceText}; availability fit {windowFit:0.##}.",
                approximate));
        }
        return output.OrderByDescending(x => x.Score).ThenBy(x => x.TargetId).ToArray();
    }

    private static HashSet<string> NeededSkills(Cms.Application.DTOs.RequestSummaryDto request)
    {
        var text = (request.Description ?? string.Empty).ToLowerInvariant();
        var candidates = new[] { "first aid", "driving", "medical", "logistics", "food", "shelter", "translation" };
        return candidates.Where(text.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

internal static class OperationsMatcherHelpers
{
    internal static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);

    internal static async Task<Distance?> DistanceAsync(IGeoService geo, decimal? fromLat, decimal? fromLng, double? toLat, double? toLng, CancellationToken ct)
    {
        if (!fromLat.HasValue || !fromLng.HasValue || !toLat.HasValue || !toLng.HasValue) return null;
        var result = await geo.GetDistanceAsync(
            new GeoPoint((double)fromLat.Value, (double)fromLng.Value),
            new GeoPoint(toLat.Value, toLng.Value), ct);
        return new Distance(result.Km, result.IsApproximate);
    }

    internal static bool HasAccessNeed(Cms.Application.DTOs.RequestSummaryDto request) =>
        !string.IsNullOrWhiteSpace(request.AccessibilityNeeds) &&
        !request.AccessibilityNeeds.Contains("none", StringComparison.OrdinalIgnoreCase);

    internal static string NeedText(Cms.Application.DTOs.RequestSummaryDto request, params string[] keywords)
    {
        var text = (request.AccessibilityNeeds ?? string.Empty) + " " + (request.Description ?? string.Empty);
        return keywords.Where(text.Contains).Aggregate(string.Empty, (current, next) => current + " " + next).Trim();
    }

    internal static bool MentionsAny(string haystack, string needles) =>
        needles.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(x => haystack.Contains(x, StringComparison.OrdinalIgnoreCase));

    internal static decimal ExpiryFit(DateTime? expiry)
    {
        if (expiry is null) return 1m;
        var days = (expiry.Value - DateTime.UtcNow).TotalDays;
        return days <= 0 ? 0m : Clamp((decimal)days / 30m);
    }

    internal static decimal EstimateQuantity(Cms.Application.DTOs.RequestSummaryDto request)
    {
        var match = Regex.Match(request.Description ?? string.Empty, @"\b(\d+(?:\.\d+)?)\s*(?:packs?|kits?|litres?|units?|items?)?\b", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && value > 0) return value;
        return Math.Max(1, request.HouseholdSize);
    }

    internal static bool MatchesCategory(string resourceTypeName, Category category)
    {
        var name = resourceTypeName.ToLowerInvariant();
        return category switch
        {
            Category.Food => name.Contains("food") || name.Contains("water") || name.Contains("nutrition"),
            Category.Medicine => name.Contains("medical") || name.Contains("medicine") || name.Contains("kit"),
            Category.Shelter => name.Contains("shelter") || name.Contains("tarp") || name.Contains("blanket"),
            Category.Transport => name.Contains("transport") || name.Contains("fuel") || name.Contains("vehicle"),
            Category.Accessibility => name.Contains("access") || name.Contains("wheelchair") || name.Contains("assist"),
            _ => true
        };
    }

    internal static string[] ParseArray(string? value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value ?? "[]");
            return doc.RootElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray();
        }
        catch { return []; }
    }

    internal record Distance(double Km, bool IsApproximate);
}
