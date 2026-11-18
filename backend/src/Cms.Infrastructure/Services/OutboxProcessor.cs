using Cms.Application.Contracts;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Background dispatcher for the transactional outbox. Every iteration claims a
/// batch of due <see cref="OutboxMessageStatus.Pending"/> or
/// <see cref="OutboxMessageStatus.Failed"/> rows, invokes the registered
/// <see cref="IOutboxHandler"/> and records the outcome. Failed messages are
/// retried with backoff and dead-lettered after <c>Outbox:MaxRetries</c>
/// attempts. Set <c>Outbox:ProcessorEnabled</c> to <c>false</c> to disable it
/// (integration tests do this because they use an in-memory database).
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    public const string ProcessorEnabledKey = "Outbox:ProcessorEnabled";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled())
        {
            _logger.LogInformation("Outbox processor is disabled by configuration.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(
            Math.Max(1, _configuration.GetValue("Outbox:PollIntervalSeconds", 5)));

        _logger.LogInformation(
            "Outbox processor started with a {PollIntervalSeconds}s poll interval.",
            pollInterval.TotalSeconds);

        using var timer = new PeriodicTimer(pollInterval);

        do
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox processor iteration failed.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    /// <summary>
    /// Runs one recovery-and-dispatch iteration. Public so tests can drive the
    /// processor without waiting for the timer.
    /// </summary>
    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await RecoverStaleMessagesAsync(now, cancellationToken);

        var batchSize = Math.Clamp(
            _configuration.GetValue("Outbox:BatchSize", 20),
            1,
            200);

        List<Guid> dueIds;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            dueIds = await db.OutboxMessages
                .AsNoTracking()
                .Where(message =>
                    (message.Status == OutboxMessageStatus.Pending ||
                     message.Status == OutboxMessageStatus.Failed) &&
                    (message.NextRetryAt == null || message.NextRetryAt <= now))
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Take(batchSize)
                .Select(message => message.Id)
                .ToListAsync(cancellationToken);
        }

        var processed = 0;

        foreach (var id in dueIds)
        {
            if (!await TryClaimAsync(id, cancellationToken))
            {
                continue;
            }

            if (await TryHandleAsync(id, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

    private async Task<bool> TryClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var message = await db.OutboxMessages
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (message is null ||
            message.Status is not (OutboxMessageStatus.Pending or OutboxMessageStatus.Failed))
        {
            return false;
        }

        message.Status = OutboxMessageStatus.Processing;
        message.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> TryHandleAsync(Guid id, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var message = await db.OutboxMessages
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (message is null || message.Status != OutboxMessageStatus.Processing)
        {
            return false;
        }

        try
        {
            var handler = scope.ServiceProvider
                .GetServices<IOutboxHandler>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Type,
                    message.Type,
                    StringComparison.Ordinal));

            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"No outbox handler is registered for event type '{message.Type}'.");
            }

            await handler.HandleAsync(message, cancellationToken);

            var processedAt = DateTime.UtcNow;
            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAt = processedAt;
            message.NextRetryAt = null;
            message.ErrorMessage = null;
            message.UpdatedAt = processedAt;

            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Outbox message {OutboxMessageId} of type {OutboxEventType} failed.",
                message.Id,
                message.Type);

            // The handler scope is discarded without saving, so any partial
            // handler changes are not persisted. Failure state is written by a
            // fresh scope.
            await MarkFailedAsync(id, exception, DateTime.UtcNow, cancellationToken);
            return false;
        }
    }

    private async Task MarkFailedAsync(
        Guid id,
        Exception exception,
        DateTime now,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var message = await db.OutboxMessages
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (message is null)
        {
            return;
        }

        ApplyFailure(message, exception, now);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecoverStaleMessagesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var staleMinutes = Math.Max(
            1,
            _configuration.GetValue("Outbox:StaleProcessingMinutes", 10));
        var threshold = now.AddMinutes(-staleMinutes);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var staleMessages = await db.OutboxMessages
            .Where(message =>
                message.Status == OutboxMessageStatus.Processing &&
                ((message.UpdatedAt != null && message.UpdatedAt < threshold) ||
                 (message.UpdatedAt == null && message.CreatedAt < threshold)))
            .ToListAsync(cancellationToken);

        if (staleMessages.Count == 0)
        {
            return;
        }

        var exception = new TimeoutException(
            $"The outbox processing lease expired after {staleMinutes} minutes.");

        foreach (var message in staleMessages)
        {
            ApplyFailure(message, exception, now, respectRetryDelay: false);
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Recovered {StaleOutboxMessageCount} stale outbox message(s).",
            staleMessages.Count);
    }

    private void ApplyFailure(
        OutboxMessage message,
        Exception exception,
        DateTime now,
        bool respectRetryDelay = true)
    {
        var maxRetries = Math.Max(1, _configuration.GetValue("Outbox:MaxRetries", 3));

        message.RetryCount += 1;
        message.ErrorMessage = Truncate(exception.Message, 4000);
        message.UpdatedAt = now;

        if (message.RetryCount >= maxRetries)
        {
            message.Status = OutboxMessageStatus.DeadLettered;
            message.NextRetryAt = null;
            message.ProcessedAt = null;
            return;
        }

        message.Status = OutboxMessageStatus.Failed;
        message.NextRetryAt = respectRetryDelay
            ? now.Add(CalculateRetryDelay(message.RetryCount))
            : now;
    }

    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        var baseSeconds = Math.Max(
            0,
            _configuration.GetValue("Outbox:RetryDelaySeconds", 30));

        if (baseSeconds == 0)
        {
            return TimeSpan.Zero;
        }

        var multiplier = Math.Pow(2, Math.Max(0, retryCount - 1));
        var seconds = Math.Min(baseSeconds * multiplier, TimeSpan.FromHours(1).TotalSeconds);

        return TimeSpan.FromSeconds(seconds);
    }

    private bool IsEnabled() => _configuration.GetValue(ProcessorEnabledKey, true);

    private static async Task<bool> WaitForNextTickAsync(
        PeriodicTimer timer,
        CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
