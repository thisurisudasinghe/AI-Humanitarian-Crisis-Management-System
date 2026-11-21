using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Outbox;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Integration;

public sealed class OutboxEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public OutboxEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dead_letter_endpoint_rejects_anonymous_callers()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/outbox/dead");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dead_letter_endpoint_rejects_non_admin_callers()
    {
        await _factory.EnsureSeededAsync();
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.OfficerEmail);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await citizen.GetAsync("/api/v1/admin/outbox/dead")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await officer.GetAsync("/api/v1/admin/outbox/dead")).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_dead_lettered_messages_without_payload_or_idempotency_key()
    {
        await _factory.EnsureSeededAsync();

        var message = await SeedDeadLetteredAsync();

        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var response = await admin.GetAsync(
            $"/api/v1/admin/outbox/dead?type={Uri.EscapeDataString(message.Type)}&page=1&pageSize=50");

        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("payload", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotencyKey", raw, StringComparison.OrdinalIgnoreCase);

        var result = JsonSerializer.Deserialize<PagedResultDto<OutboxMessageDto>>(raw, JsonOptions);

        Assert.NotNull(result);
        var item = Assert.Single(result!.Items, entry => entry.Id == message.Id);
        Assert.Equal(message.Type, item.Type);
        Assert.Equal(message.EntityId, item.EntityId);
        Assert.Equal(OutboxMessageStatus.DeadLettered, item.Status);
        Assert.Equal(3, item.RetryCount);
        Assert.Equal("handler failed", item.ErrorMessage);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Admin_can_page_dead_lettered_messages()
    {
        await _factory.EnsureSeededAsync();
        await SeedDeadLetteredAsync();
        await SeedDeadLetteredAsync();

        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var response = await admin.GetAsync("/api/v1/admin/outbox/dead?page=1&pageSize=1");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<OutboxMessageDto>>(JsonOptions);

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.True(result.TotalCount >= 2);
    }

    [Fact]
    public async Task Legacy_dead_letter_controller_uses_admin_policy_and_hides_payload()
    {
        await _factory.EnsureSeededAsync();
        var message = await SeedDeadLetteredAsync();

        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await citizen.GetAsync("/api/v1/admin/dead-letters")).StatusCode);

        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var response = await admin.GetAsync("/api/v1/admin/dead-letters?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("payload", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotencyKey", raw, StringComparison.OrdinalIgnoreCase);

        var result = JsonSerializer.Deserialize<PagedResultDto<OutboxMessageDto>>(raw, JsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result!.Items, item => item.Id == message.Id);

        var missingRetry = await admin.PostAsync(
            $"/api/v1/admin/dead-letters/{Guid.NewGuid()}/retry", null);
        Assert.Equal(HttpStatusCode.NotFound, missingRetry.StatusCode);

        var retry = await admin.PostAsync(
            $"/api/v1/admin/dead-letters/{message.Id}/retry", null);
        retry.EnsureSuccessStatusCode();
    }

    private async Task<OutboxMessage> SeedDeadLetteredAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var message = new OutboxMessage
        {
            Type = $"Test.DeadLetter.{Guid.NewGuid():N}",
            EntityId = Guid.NewGuid(),
            Payload = """{"secret":"not exposed"}""",
            IdempotencyKey = $"dead:{Guid.NewGuid():N}",
            Status = OutboxMessageStatus.DeadLettered,
            RetryCount = 3,
            ErrorMessage = "handler failed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        return message;
    }
}
