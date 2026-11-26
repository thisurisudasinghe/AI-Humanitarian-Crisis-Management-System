using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.DTOs.Public;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cms.Tests.Integration;

/// <summary>
/// End-to-end coverage for the explicit publication layer behind the anonymous
/// live map: only whitelisted fields are returned, hidden/draft/expired alerts
/// never leak, and coordinator publication is audited.
/// </summary>
public sealed class PublicMapWorkflowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Anonymous_map_excludes_hidden_and_expired_alerts()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();

        var now = DateTime.UtcNow;
        var publishedId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            db.Set<Alert>().AddRange(
                NewAlert(publishedId, "INTERNAL_TITLE_VISIBLE", "INTERNAL_BODY_VISIBLE",
                    AlertStatus.Published, true, now.AddDays(1), now.AddMinutes(-10)),
                NewAlert(Guid.NewGuid(), "INTERNAL_TITLE_DRAFT", "INTERNAL_BODY_DRAFT",
                    AlertStatus.Draft, false, now.AddDays(1), now.AddMinutes(-9)),
                NewAlert(Guid.NewGuid(), "INTERNAL_TITLE_HIDDEN", "INTERNAL_BODY_HIDDEN",
                    AlertStatus.Published, false, now.AddDays(1), now.AddMinutes(-8)),
                NewAlert(Guid.NewGuid(), "INTERNAL_TITLE_EXPIRED", "INTERNAL_BODY_EXPIRED",
                    AlertStatus.Published, true, now.AddMinutes(-1), now.AddMinutes(-7)));

            db.Shelters.AddRange(
                new Shelter
                {
                    Id = Guid.NewGuid(),
                    Name = "Approved shelter",
                    Capacity = 40,
                    Occupancy = 12,
                    Services = "[\"food\"]",
                    Facilities = "INTERNAL_FACILITIES",
                    AccessibilityFeatures = "INTERNAL_ACCESSIBILITY",
                    Lat = 6.9,
                    Lng = 79.86,
                    OrgId = Guid.NewGuid(),
                    Approved = true
                },
                new Shelter
                {
                    Id = Guid.NewGuid(),
                    Name = "Unapproved shelter",
                    Capacity = 20,
                    Occupancy = 1,
                    Services = "[]",
                    Lat = 6.91,
                    Lng = 79.87,
                    OrgId = Guid.NewGuid(),
                    Approved = false
                });

            db.Set<ServiceCentre>().AddRange(
                NewServiceCentre("Public aid centre", true, true),
                NewServiceCentre("Hidden aid centre", true, false),
                NewServiceCentre("Inactive aid centre", false, true));

            db.Set<Cms.Domain.Entities.Cases.Request>().Add(new Cms.Domain.Entities.Cases.Request
            {
                Id = Guid.NewGuid(),
                RequesterUserId = Guid.NewGuid(),
                AssignedToUserId = Guid.NewGuid(),
                Category = Category.Food,
                Urgency = Urgency.High,
                Description = "INTERNAL_REQUEST_DESCRIPTION",
                Address = "INTERNAL_REQUEST_ADDRESS",
                Lat = 6.92m,
                Lng = 79.88m,
                HouseholdSize = 2,
                SubmittedAt = now.AddHours(-1),
                Status = RequestStatus.Submitted
            });

            await db.SaveChangesAsync();
        }

        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/v1/public/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        foreach (var marker in new[]
                 {
                     "INTERNAL_TITLE", "INTERNAL_BODY", "INTERNAL_REQUEST_ADDRESS",
                     "INTERNAL_REQUEST_DESCRIPTION", "INTERNAL_FACILITIES",
                     "INTERNAL_ACCESSIBILITY", "requesterUserId", "assignedToUserId",
                     "orgId", "crisisId", "publicVisible", "isActive", "isPublic"
                 })
        {
            Assert.DoesNotContain(marker, json, StringComparison.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("updatedAt", out _));

        var incidents = root.GetProperty("incidents").EnumerateArray().ToList();
        var incident = Assert.Single(incidents);
        Assert.Equal(publishedId.ToString(), incident.GetProperty("id").GetString());
        Assert.Equal("Public flood advisory", incident.GetProperty("title").GetString());
        Assert.Equal("Move to higher ground", incident.GetProperty("summary").GetString());
        Assert.Equal("Flood", incident.GetProperty("type").GetString());
        Assert.Equal("High", incident.GetProperty("severity").GetString());
        Assert.Equal("Published", incident.GetProperty("status").GetString());
        Assert.Equal("Colombo", incident.GetProperty("district").GetString());
        Assert.Equal(6.93, incident.GetProperty("latitude").GetDouble(), 6);
        Assert.Equal(79.86, incident.GetProperty("longitude").GetDouble(), 6);
        Assert.True(incident.TryGetProperty("publishedAt", out _));
        Assert.True(incident.TryGetProperty("expiresAt", out _));
        foreach (var forbidden in new[]
                 {
                     "body", "crisisId", "publishedByUserId", "publicVisible",
                     "createdAt", "updatedAt", "address"
                 })
        {
            Assert.False(incident.TryGetProperty(forbidden, out _), forbidden);
        }

        var shelter = Assert.Single(root.GetProperty("shelters").EnumerateArray());
        Assert.Equal("Approved shelter", shelter.GetProperty("name").GetString());
        Assert.Equal(40, shelter.GetProperty("capacity").GetInt32());
        Assert.Equal(12, shelter.GetProperty("occupancy").GetInt32());
        foreach (var forbidden in new[] { "orgId", "approved", "facilities", "accessibilityFeatures" })
        {
            Assert.False(shelter.TryGetProperty(forbidden, out _), forbidden);
        }

        var aidLocation = Assert.Single(root.GetProperty("aidLocations").EnumerateArray());
        Assert.Equal("Public aid centre", aidLocation.GetProperty("name").GetString());
        Assert.Equal("1 Main St", aidLocation.GetProperty("address").GetString());
        foreach (var forbidden in new[] { "crisisId", "isActive", "publicVisible", "createdAt", "updatedAt" })
        {
            Assert.False(aidLocation.TryGetProperty(forbidden, out _), forbidden);
        }
    }

    [Fact]
    public async Task Anonymous_map_handles_null_and_blank_severity_without_crashing()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();
        var now = DateTime.UtcNow;
        var nullSeverityId = Guid.NewGuid();
        var blankSeverityId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            db.Set<Alert>().AddRange(
                NewAlert(nullSeverityId, "PRIVATE_NULL_TITLE", "PRIVATE_NULL_BODY",
                    AlertStatus.Draft, false, null, now.AddMinutes(-3)),
                NewAlert(blankSeverityId, "PRIVATE_BLANK_TITLE", "PRIVATE_BLANK_BODY",
                    AlertStatus.Draft, false, null, now.AddMinutes(-2)));
            await db.SaveChangesAsync();
        }

        using var coordinator = (await CreateCoordinatorAsync(factory)).Client;
        var publish = await coordinator.PostAsJsonAsync(
            $"/api/v1/alerts/{blankSeverityId}/map-publication",
            new PublishMapAlertDto
            {
                Visible = true,
                PublicTitle = "Blank severity advisory",
                PublicSummary = "Severity was intentionally not supplied.",
                CrisisType = "Flood",
                Severity = "   ",
                District = "Colombo",
                Latitude = 6.93,
                Longitude = 79.86,
                ExpiresAt = now.AddDays(1)
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var nullSeverityAlert = await db.Set<Alert>().SingleAsync(a => a.Id == nullSeverityId);
            nullSeverityAlert.PublicVisible = true;
            nullSeverityAlert.PublicTitle = "Null severity advisory";
            nullSeverityAlert.PublicSummary = "Severity is absent in the database.";
            nullSeverityAlert.PublicType = "Flood";
            nullSeverityAlert.Severity = null;
            nullSeverityAlert.District = "Colombo";
            nullSeverityAlert.Latitude = 6.93;
            nullSeverityAlert.Longitude = 79.86;
            nullSeverityAlert.PublishedAt = now.AddMinutes(-1);
            nullSeverityAlert.UpdatedAt = now;

            var blankSeverityAlert = await db.Set<Alert>().SingleAsync(a => a.Id == blankSeverityId);
            Assert.Null(blankSeverityAlert.Severity);
            await db.SaveChangesAsync();
        }

        using var anonymous = factory.CreateClient();
        var map = await anonymous.GetFromJsonAsync<PublicMapDto>("/api/v1/public/map", JsonOptions);
        Assert.NotNull(map);
        Assert.Equal(2, map!.Incidents.Count);
        Assert.All(map.Incidents, incident => Assert.Null(incident.Severity));

        var json = await anonymous.GetStringAsync("/api/v1/public/map");
        foreach (var marker in new[]
                 { "PRIVATE_NULL_TITLE", "PRIVATE_NULL_BODY", "PRIVATE_BLANK_TITLE", "PRIVATE_BLANK_BODY" })
        {
            Assert.DoesNotContain(marker, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Anonymous_public_alerts_return_only_public_fields()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();
        var now = DateTime.UtcNow;
        var visibleId = Guid.NewGuid();
        var hiddenId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var visible = NewAlert(visibleId, "PRIVATE_ALERT_TITLE", "PRIVATE_ALERT_BODY",
                AlertStatus.Published, true, now.AddDays(1), now.AddMinutes(-10));
            visible.PublicTitle = "SAFE_PUBLIC_TITLE";
            visible.PublicSummary = "SAFE_PUBLIC_SUMMARY";
            var hidden = NewAlert(hiddenId, "HIDDEN_PRIVATE_TITLE", "HIDDEN_PRIVATE_BODY",
                AlertStatus.Published, false, now.AddDays(1), now.AddMinutes(-9));
            hidden.PublicTitle = "HIDDEN_PUBLIC_TITLE";
            hidden.PublicSummary = "HIDDEN_PUBLIC_SUMMARY";
            db.Set<Alert>().AddRange(visible, hidden);
            await db.SaveChangesAsync();
        }

        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/v1/public/alerts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("SAFE_PUBLIC_TITLE", json, StringComparison.Ordinal);
        Assert.Contains("SAFE_PUBLIC_SUMMARY", json, StringComparison.Ordinal);
        foreach (var marker in new[]
                 {
                     "PRIVATE_ALERT_TITLE", "PRIVATE_ALERT_BODY", "HIDDEN_PRIVATE_TITLE",
                     "HIDDEN_PRIVATE_BODY", "HIDDEN_PUBLIC_TITLE", "HIDDEN_PUBLIC_SUMMARY",
                     hiddenId.ToString()
                 })
        {
            Assert.DoesNotContain(marker, json, StringComparison.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(json);
        var alert = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(visibleId.ToString(), alert.GetProperty("id").GetString());
        Assert.Equal("SAFE_PUBLIC_TITLE", alert.GetProperty("title").GetString());
        Assert.Equal("SAFE_PUBLIC_SUMMARY", alert.GetProperty("summary").GetString());
        Assert.Equal("Flood", alert.GetProperty("type").GetString());
        Assert.Equal("High", alert.GetProperty("severity").GetString());
        Assert.Equal("Published", alert.GetProperty("status").GetString());
        foreach (var forbidden in new[]
                 { "body", "crisisId", "createdAt", "publishedByUserId", "notes", "address" })
        {
            Assert.False(alert.TryGetProperty(forbidden, out _), forbidden);
        }
    }

    [Fact]
    public async Task Coordinator_can_list_publish_and_hide_an_alert_with_audit()
    {
        using var factory = new CmsWebApplicationFactory();
        var coordinatorContext = await CreateCoordinatorAsync(factory);
        using var coordinator = coordinatorContext.Client;
        var coordinatorEmail = coordinatorContext.Email;
        var alertId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            db.Set<Alert>().Add(NewAlert(
                alertId,
                "PRIVATE_EDITORIAL_TITLE",
                "PRIVATE_EDITORIAL_BODY",
                AlertStatus.Draft,
                false,
                null,
                DateTime.UtcNow.AddMinutes(-20)));
            await db.SaveChangesAsync();
        }

        var listResponse = await coordinator.GetAsync("/api/v1/alerts");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var listed = Assert.Single(listJson.RootElement.EnumerateArray());
        Assert.Equal(alertId.ToString(), listed.GetProperty("id").GetString());
        Assert.Equal("PRIVATE_EDITORIAL_TITLE", listed.GetProperty("title").GetString());
        Assert.Equal("PRIVATE_EDITORIAL_BODY", listed.GetProperty("body").GetString());
        Assert.Equal("Draft", listed.GetProperty("status").GetString());
        Assert.False(listed.GetProperty("publicVisible").GetBoolean());
        foreach (var expected in new[]
                 {
                     "crisisId", "publishedAt", "publicTitle", "publicSummary", "crisisType",
                     "severity", "district", "latitude", "longitude", "expiresAt", "publishedByUserId"
                 })
        {
            Assert.True(listed.TryGetProperty(expected, out _), expected);
        }

        var publish = await coordinator.PostAsJsonAsync(
            $"/api/v1/alerts/{alertId}/map-publication",
            new PublishMapAlertDto
            {
                Visible = true,
                PublicTitle = "Road closure",
                PublicSummary = "Use the alternate route via Galle Road.",
                CrisisType = "Flood",
                Severity = "Medium",
                District = "Colombo",
                Latitude = 6.93,
                Longitude = 79.86,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        Guid? publishedBy = null;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var stored = await db.Set<Alert>().SingleAsync(a => a.Id == alertId);
            Assert.True(stored.PublicVisible);
            // Map publication is explicit and must not activate the legacy public alerts feed.
            Assert.Equal(AlertStatus.Draft, stored.Status);
            Assert.NotNull(stored.PublishedAt);
            Assert.Equal("Road closure", stored.PublicTitle);
            Assert.Equal("Use the alternate route via Galle Road.", stored.PublicSummary);
            Assert.Equal(6.93, stored.Latitude);
            Assert.Equal(79.86, stored.Longitude);
            Assert.NotNull(stored.PublishedByUserId);
            publishedBy = stored.PublishedByUserId;

            Assert.Contains(await db.AuditEvents.ToListAsync(), audit =>
                audit.Action == "Alert.PublishedToMap" && audit.EntityId == alertId.ToString());
        }

        Assert.NotNull(publishedBy);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var protection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
            var actor = await db.Users.SingleAsync(u => u.EmailHash == protection.HashEmail(coordinatorEmail));
            Assert.Equal(actor.Id, publishedBy);
        }

        using (var anonymous = factory.CreateClient())
        {
            var map = await anonymous.GetFromJsonAsync<PublicMapDto>("/api/v1/public/map", JsonOptions);
            Assert.NotNull(map);
            Assert.Contains(map!.Incidents, incident =>
                incident.Id == alertId && incident.Title == "Road closure");

            var legacyResponse = await anonymous.GetAsync("/api/v1/public/alerts");
            Assert.Equal(HttpStatusCode.OK, legacyResponse.StatusCode);
            var legacyFeed = await legacyResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("PRIVATE_EDITORIAL_TITLE", legacyFeed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PRIVATE_EDITORIAL_BODY", legacyFeed, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Road closure", legacyFeed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("title\":\"PRIVATE", legacyFeed, StringComparison.OrdinalIgnoreCase);
        }

        var hide = await coordinator.PostAsJsonAsync(
            $"/api/v1/alerts/{alertId}/map-publication",
            new PublishMapAlertDto { Visible = false },
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var hidden = await db.Set<Alert>().SingleAsync(a => a.Id == alertId);
            Assert.False(hidden.PublicVisible);
            Assert.Null(hidden.PublicTitle);
            Assert.Null(hidden.PublicSummary);
            Assert.Equal("PRIVATE_EDITORIAL_TITLE", hidden.Title);
            Assert.Equal("PRIVATE_EDITORIAL_BODY", hidden.Body);
            Assert.Contains(await db.AuditEvents.ToListAsync(), audit =>
                audit.Action == "Alert.HiddenFromMap" && audit.EntityId == alertId.ToString());
        }

        using (var anonymous = factory.CreateClient())
        {
            var map = await anonymous.GetFromJsonAsync<PublicMapDto>("/api/v1/public/map", JsonOptions);
            Assert.NotNull(map);
            Assert.DoesNotContain(map!.Incidents, incident => incident.Id == alertId);

            var legacyFeed = await anonymous.GetStringAsync("/api/v1/public/alerts");
            Assert.DoesNotContain("PRIVATE_EDITORIAL_TITLE", legacyFeed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PRIVATE_EDITORIAL_BODY", legacyFeed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Road closure", legacyFeed, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(alertId.ToString(), legacyFeed, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Publication_rejects_invalid_coordinates_and_requires_coordinator()
    {
        using var factory = new CmsWebApplicationFactory();
        using var coordinator = (await CreateCoordinatorAsync(factory)).Client;
        var alertId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            db.Set<Alert>().Add(NewAlert(
                alertId, "Private", "Private body", AlertStatus.Draft, false, null, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var invalid = await coordinator.PostAsJsonAsync(
            $"/api/v1/alerts/{alertId}/map-publication",
            new PublishMapAlertDto
            {
                Visible = true,
                PublicTitle = "Out of range",
                PublicSummary = "Invalid coordinates",
                CrisisType = "Flood",
                Latitude = 120,
                Longitude = 79.86
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var invalidLongitude = await coordinator.PostAsJsonAsync(
            $"/api/v1/alerts/{alertId}/map-publication",
            new PublishMapAlertDto
            {
                Visible = true,
                PublicTitle = "Out of range",
                PublicSummary = "Invalid coordinates",
                CrisisType = "Flood",
                Latitude = 6.93,
                Longitude = -200
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidLongitude.StatusCode);

        using var scopeAfter = factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<CmsDbContext>();
        Assert.False((await dbAfter.Set<Alert>().SingleAsync(a => a.Id == alertId)).PublicVisible);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/v1/alerts/{alertId}/map-publication",
                new PublishMapAlertDto { Visible = false },
                JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/alerts")).StatusCode);
    }

    [Fact]
    public async Task Coordinator_can_toggle_service_centre_public_visibility_with_audit()
    {
        using var factory = new CmsWebApplicationFactory();
        using var coordinator = (await CreateCoordinatorAsync(factory)).Client;
        var centreId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            db.Set<ServiceCentre>().Add(new ServiceCentre
            {
                Id = centreId,
                Name = "Temporary aid centre",
                Address = "1 Main St",
                Lat = 6.9m,
                Lng = 79.9m,
                Services = "Food",
                IsActive = true,
                PublicVisible = true
            });
            await db.SaveChangesAsync();
        }

        var response = await coordinator.PatchAsJsonAsync(
            $"/api/v1/service-centres/{centreId}/public-visibility",
            new SetServiceCentreVisibilityDto { IsPublic = false },
            JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            Assert.False((await db.Set<ServiceCentre>().SingleAsync(s => s.Id == centreId)).PublicVisible);
            Assert.Contains(await db.AuditEvents.ToListAsync(), audit =>
                audit.Action == "ServiceCentre.PublicVisibilityChanged"
                && audit.EntityId == centreId.ToString());
        }

        using var anonymous = factory.CreateClient();
        var map = await anonymous.GetFromJsonAsync<PublicMapDto>("/api/v1/public/map", JsonOptions);
        Assert.NotNull(map);
        Assert.DoesNotContain(map!.AidLocations, location => location.Id == centreId);

        var publicJson = await anonymous.GetStringAsync("/api/v1/public/service-centres");
        Assert.DoesNotContain(centreId.ToString(), publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/service-centres")).StatusCode);

        var staffJson = await coordinator.GetStringAsync("/api/v1/service-centres");
        using var staffDocument = JsonDocument.Parse(staffJson);
        var staffCentre = Assert.Single(staffDocument.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetString() == centreId.ToString());
        Assert.False(staffCentre.GetProperty("publicVisible").GetBoolean());
        Assert.True(staffCentre.GetProperty("isActive").GetBoolean());
    }

    private static Alert NewAlert(
        Guid id,
        string privateTitle,
        string privateBody,
        AlertStatus status,
        bool publicVisible,
        DateTime? expiresAt,
        DateTime createdAt) => new()
        {
            Id = id,
            Title = privateTitle,
            Body = privateBody,
            Status = status,
            PublishedAt = publicVisible ? createdAt.AddMinutes(1) : null,
            PublicVisible = publicVisible,
            PublicTitle = publicVisible ? "Public flood advisory" : null,
            PublicSummary = publicVisible ? "Move to higher ground" : null,
            PublicType = publicVisible ? "Flood" : null,
            Severity = publicVisible ? "High" : null,
            District = publicVisible ? "Colombo" : null,
            Latitude = publicVisible ? 6.93 : null,
            Longitude = publicVisible ? 79.86 : null,
            PublicExpiresAt = expiresAt,
            CreatedAt = createdAt
        };

    private static ServiceCentre NewServiceCentre(string name, bool isActive, bool publicVisible) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Address = "1 Main St",
        Lat = 6.9m,
        Lng = 79.9m,
        Services = "Food",
        OpeningHours = "08:00-17:00",
        IsActive = isActive,
        PublicVisible = publicVisible
    };

    private static async Task<(HttpClient Client, string Email)> CreateCoordinatorAsync(
        CmsWebApplicationFactory factory)
    {
        await factory.EnsureSeededAsync();
        var email = $"map.coordinator.{Guid.NewGuid():N}@tests.local";

        using var registration = factory.CreateClient();
        var created = await registration.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Map Coordinator",
            email,
            phoneNumber = "0771234567",
            password = TestSecrets.Password
        });
        created.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
        var role = await db.Roles.SingleAsync(r => r.Name == RoleNames.EmergencyCoordinator);
        var user = await db.Users.SingleAsync(u => u.EmailHash == emailProtection.HashEmail(email));
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        var client = await factory.CreateAuthenticatedClientAsync(email);
        return (client, email);
    }
}
