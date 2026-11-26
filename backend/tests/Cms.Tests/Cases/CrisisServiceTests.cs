using System.Text.Json;
using Cms.Application.Cases.DTOs;
using Cms.Application.DTOs.Public;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class CrisisServiceTests
{
    [Fact]
    public async Task Draft_alert_is_public_only_after_explicit_map_publication()
    {
        using var context = TestContext.Create();
        var coordinator = Coordinator();
        var now = DateTime.UtcNow;
        var service = CreateService(context, coordinator, now);
        var crisisId = await service.CreateCrisisAsync(new CreateCrisisDto
        {
            Name = "Flood",
            Type = "Flood",
            StartedAt = now
        });
        var alertId = await service.CreateAlertAsync(crisisId, new CreateAlertDto
        {
            Title = "PRIVATE_EDITORIAL_TITLE",
            Body = "PRIVATE_EDITORIAL_BODY"
        });

        Assert.Empty(await service.ListPublicAlertsAsync());

        // Editorial publication must not disclose private staff-authored content.
        await service.UpdateAlertAsync(alertId, new UpdateAlertDto { Publish = true });
        Assert.Empty(await service.ListPublicAlertsAsync());

        await service.PublishAlertToMapAsync(alertId, new PublishMapAlertDto
        {
            Visible = true,
            PublicTitle = "Public road closure",
            PublicSummary = "Use the alternate route.",
            CrisisType = "Flood",
            Severity = "Medium",
            District = "Colombo",
            Latitude = 6.93,
            Longitude = 79.86,
            ExpiresAt = now.AddDays(1)
        });

        var published = await service.ListPublicAlertsAsync();
        var alert = Assert.Single(published);
        Assert.Equal(alertId, alert.Id);
        Assert.Equal("Public road closure", alert.Title);
        Assert.Equal("Use the alternate route.", alert.Summary);
        Assert.Equal("Flood", alert.Type);
        Assert.Equal("Published", alert.Status);
        Assert.NotNull(alert.PublishedAt);

        await service.PublishAlertToMapAsync(alertId, new PublishMapAlertDto { Visible = false });
        Assert.Empty(await service.ListPublicAlertsAsync());
    }

    [Fact]
    public async Task Public_alerts_project_only_explicitly_approved_fields()
    {
        using var context = TestContext.Create();
        var service = CreateService(context, Coordinator(), DateTime.UtcNow);
        var crisisId = await service.CreateCrisisAsync(new CreateCrisisDto
        {
            Name = "Flood",
            Type = "Flood",
            StartedAt = DateTime.UtcNow
        });
        var alertId = await service.CreateAlertAsync(crisisId, new CreateAlertDto
        {
            Title = "PRIVATE_INTERNAL_TITLE",
            Body = "PRIVATE_INTERNAL_BODY"
        });
        await service.UpdateAlertAsync(alertId, new UpdateAlertDto { Publish = true });
        await service.PublishAlertToMapAsync(alertId, new PublishMapAlertDto
        {
            Visible = true,
            PublicTitle = "SAFE_PUBLIC_TITLE",
            PublicSummary = "SAFE_PUBLIC_SUMMARY",
            CrisisType = "Flood",
            Severity = "High",
            District = "Colombo",
            Latitude = 6.9,
            Longitude = 79.9
        });

        var projected = Assert.Single(await service.ListPublicAlertsAsync());
        Assert.Equal("SAFE_PUBLIC_TITLE", projected.Title);
        Assert.Equal("SAFE_PUBLIC_SUMMARY", projected.Summary);

        var json = JsonSerializer.Serialize(projected, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("PRIVATE_INTERNAL_TITLE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_INTERNAL_BODY", json, StringComparison.Ordinal);
        Assert.DoesNotContain("crisisId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publishedByUserId", json, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "title", "summary", "type", "severity", "status",
            "district", "latitude", "longitude", "publishedAt", "expiresAt"
        };
        Assert.All(document.RootElement.EnumerateObject(),
            property => Assert.Contains(property.Name, allowed));
    }

    [Fact]
    public async Task Citizen_cannot_create_crisis_or_alert()
    {
        using var context = TestContext.Create();
        var citizen = new FakeCurrentUser { Roles = new[] { CasesRoles.Citizen } };
        var service = CreateService(context, citizen, DateTime.UtcNow);

        var crisisEx = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateCrisisAsync(new CreateCrisisDto
            {
                Name = "Flood",
                Type = "Flood",
                StartedAt = DateTime.UtcNow
            }));
        var alertEx = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAlertAsync(Guid.NewGuid(), new CreateAlertDto
            {
                Title = "Boil water",
                Body = "Boil water before drinking."
            }));

        Assert.Equal("COORDINATOR_ROLE_REQUIRED", crisisEx.Code);
        Assert.Equal("COORDINATOR_ROLE_REQUIRED", alertEx.Code);
        Assert.Equal(0, await context.Set<Crisis>().CountAsync());
        Assert.Equal(0, await context.Set<Alert>().CountAsync());
    }

    [Fact]
    public async Task Public_service_centres_expose_only_active_publicly_visible_centres()
    {
        using var context = TestContext.Create();
        await context.Set<ServiceCentre>().AddRangeAsync(
            new ServiceCentre
            {
                Id = Guid.NewGuid(),
                Name = "Active centre",
                Address = "1 Main St",
                Lat = 6.9m,
                Lng = 79.9m,
                Services = "Food",
                IsActive = true,
                PublicVisible = true
            },
            new ServiceCentre
            {
                Id = Guid.NewGuid(),
                Name = "Hidden centre",
                Address = "2 Main St",
                Lat = 6.9m,
                Lng = 79.9m,
                Services = "Food",
                IsActive = true,
                PublicVisible = false
            },
            new ServiceCentre
            {
                Id = Guid.NewGuid(),
                Name = "Closed centre",
                Address = "3 Main St",
                Lat = 6.9m,
                Lng = 79.9m,
                Services = "Food",
                IsActive = false,
                PublicVisible = true
            });
        await context.SaveChangesAsync();
        var service = CreateService(context, Coordinator(), DateTime.UtcNow);

        var result = await service.ListPublicServiceCentresAsync();

        var centre = Assert.Single(result);
        Assert.Equal("Active centre", centre.Name);
    }

    [Fact]
    public async Task Staff_service_centres_include_hidden_and_inactive_centres()
    {
        using var context = TestContext.Create();
        await context.Set<ServiceCentre>().AddRangeAsync(
            NewServiceCentre("Public centre", true, true),
            NewServiceCentre("Hidden centre", true, false),
            NewServiceCentre("Closed centre", false, true));
        await context.SaveChangesAsync();
        var service = CreateService(context, Coordinator(), DateTime.UtcNow);

        var result = await service.ListStaffServiceCentresAsync();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, centre => centre.Name == "Hidden centre" && !centre.PublicVisible);
        Assert.Contains(result, centre => centre.Name == "Closed centre" && !centre.IsActive);
    }

    [Fact]
    public async Task Summary_counts_open_and_overdue_requests()
    {
        using var context = TestContext.Create();
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var crisisId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        await context.Set<Crisis>().AddAsync(new Crisis
        {
            Id = crisisId,
            Name = "Flood",
            Type = "Flood",
            StartedAt = now
        });
        await context.Set<Area>().AddAsync(new Area
        {
            Id = areaId,
            CrisisId = crisisId,
            Name = "Colombo",
            Lat = 6.9m,
            Lng = 79.9m
        });
        var recent = TestContext.NewRequest(Guid.NewGuid(), submittedAt: now.AddHours(-1));
        recent.CrisisId = crisisId;
        recent.AreaId = areaId;
        var overdue = TestContext.NewRequest(Guid.NewGuid(), submittedAt: now.AddHours(-25));
        overdue.CrisisId = crisisId;
        var closedOld = TestContext.NewRequest(
            Guid.NewGuid(),
            RequestStatus.Closed,
            submittedAt: now.AddHours(-25));
        closedOld.CrisisId = crisisId;
        await context.Set<Cms.Domain.Entities.Cases.Request>().AddRangeAsync(recent, overdue, closedOld);
        await context.SaveChangesAsync();
        var service = CreateService(context, Coordinator(), now);

        var summary = await service.GetSummaryAsync(crisisId);

        Assert.Equal(2, summary.OpenTotal);
        Assert.Equal(1, summary.OverdueTotal);
        Assert.Equal(1, summary.ByArea["Colombo"]);
        Assert.Equal(2, summary.ByArea["Unassigned"]);
    }

    private static ServiceCentre NewServiceCentre(string name, bool isActive, bool publicVisible) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Address = "1 Main St",
        Lat = 6.9m,
        Lng = 79.9m,
        Services = "Food",
        IsActive = isActive,
        PublicVisible = publicVisible
    };

    private static FakeCurrentUser Coordinator() =>
        new() { Roles = new[] { CasesRoles.EmergencyCoordinator } };

    private static CrisisService CreateService(
        CmsDbContext context,
        FakeCurrentUser user,
        DateTime now)
    {
        var date = new FixedDateTimeService(now);
        var options = TestContext.Options();
        return new CrisisService(
            context,
            user,
            date,
            new OverduePolicy(options),
            TestContext.CreateWriter(context, date, options));
    }
}
