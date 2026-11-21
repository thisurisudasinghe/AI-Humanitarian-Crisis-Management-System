using System.Text.Json;
using Cms.Application.Contracts.Operations;
using Cms.Domain.Entities.Operations;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Operations;

/// <summary>Read-only deterministic matchers. They rank candidates and never reserve or allocate.</summary>
public sealed class ResourceMatcher(CmsDbContext db, IGeoService geo) : IResourceMatcher
{
    public async Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await db.Requests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Array.Empty<MatchCandidate>();
        var typeText = request.Category.ToString();
        var rows = await (from inventory in db.Inventories.AsNoTracking()
                          join resourceType in db.ResourceTypes.AsNoTracking() on inventory.ResourceTypeId equals resourceType.Id
                          where inventory.Available > 0 && resourceType.Name.ToLower().Contains(typeText.ToLower())
                          select inventory).ToListAsync(ct);
        var output = new List<MatchCandidate>();
        foreach (var row in rows)
        {
            var distance = row.Lat.HasValue && row.Lng.HasValue
                ? await geo.GetDistanceAsync(new((double)request.Lat, (double)request.Lng), new(row.Lat.Value, row.Lng.Value), ct)
                : new DistanceResult(0, null, true);
            var stockScore = Math.Min(50m, row.Available * 5m);
            var distanceScore = distance.Km == 0 ? 20m : Math.Max(0m, 30m - (decimal)distance.Km / 10m);
            output.Add(new(row.Id, Math.Round(stockScore + distanceScore, 2),
                $"Available stock: {row.Available}; distance: {(distance.IsApproximate ? "approx. " : "")}{distance.Km:0.##} km.", distance.IsApproximate));
        }
        return output.OrderByDescending(x => x.Score).ThenBy(x => x.TargetId).ToArray();
    }
}

public sealed class ShelterRecommender(CmsDbContext db, IGeoService geo) : IShelterRecommender
{
    public async Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await db.Requests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Array.Empty<MatchCandidate>();
        var shelters = await db.Shelters.AsNoTracking().Where(x => x.Approved && x.Capacity > x.Occupancy + request.HouseholdSize).ToListAsync(ct);
        var result = new List<MatchCandidate>();
        foreach (var shelter in shelters)
        {
            var d = await geo.GetDistanceAsync(new((double)request.Lat, (double)request.Lng), new(shelter.Lat, shelter.Lng), ct);
            var freeRatio = (decimal)(shelter.Capacity - shelter.Occupancy) / Math.Max(1, shelter.Capacity);
            var score = Math.Round(Math.Max(0, 70m - (decimal)d.Km / 2m) + freeRatio * 30m, 2);
            result.Add(new(shelter.Id, score, $"Approved shelter; {shelter.Capacity - shelter.Occupancy} places free; {d.Km:0.##} km {(d.IsApproximate ? "(approximate)" : "(route)")}.", d.IsApproximate));
        }
        return result.OrderByDescending(x => x.Score).ThenBy(x => x.TargetId).ToArray();
    }
}

public sealed class VolunteerMatcher(CmsDbContext db, IGeoService geo) : IVolunteerMatcher
{
    public async Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await db.Requests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Array.Empty<MatchCandidate>();
        var now = DateTime.UtcNow;
        var volunteers = await db.Volunteers.AsNoTracking().Where(x => x.Active && x.VerificationStatus == "Verified")
            .Where(x => db.VolunteerAvailabilities.Any(a => a.VolunteerId == x.Id && a.StartsAt <= now && a.EndsAt >= now))
            .ToListAsync(ct);
        var result = new List<MatchCandidate>();
        foreach (var volunteer in volunteers)
        {
            var d = volunteer.Lat.HasValue && volunteer.Lng.HasValue
                ? await geo.GetDistanceAsync(new((double)request.Lat, (double)request.Lng), new(volunteer.Lat.Value, volunteer.Lng.Value), ct)
                : new DistanceResult(0, null, true);
            var skills = Parse(volunteer.SkillsJson);
            var skillScore = skills.Count == 0 ? 0m : Math.Min(40m, skills.Count * 10m);
            var distanceScore = d.Km == 0 ? 20m : Math.Max(0m, 60m - (decimal)d.Km);
            result.Add(new(volunteer.Id, Math.Round(skillScore + distanceScore, 2),
                $"Verified and currently available; {skills.Count} listed skills; {(d.Km == 0 ? "location unavailable" : $"{d.Km:0.##} km away") }.", d.IsApproximate));
        }
        return result.OrderByDescending(x => x.Score).ThenBy(x => x.TargetId).ToArray();
    }
    private static HashSet<string> Parse(string json)
    { try { return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new(); } catch { return new(); } }
}
