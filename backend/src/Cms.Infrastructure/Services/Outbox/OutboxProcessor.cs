using Cms.Application.Interfaces;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Services.Outbox;

public class OutboxProcessor : BackgroundService
{
    public const int MaxAttempts = 3;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleProcessingThreshold = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processor error.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes one batch. Public so it can be exercised deterministically by tests.
    /// </summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IOutboxHandlerRegistry>();

        var now = DateTime.UtcNow;

        await RecoverStaleProcessing(db, now, cancellationToken);

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var messages = await db.OutboxMessages
            .Where(x =>
                x.Status == OutboxMessageStatus.Pending &&
                (x.NextRetryAt == null || x.NextRetryAt <= now) &&
                x.Attempts < MaxAttempts)
            .OrderBy(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.ProcessingStartedAt = DateTime.UtcNow;
            message.UpdatedAt = message.ProcessingStartedAt;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var handler = registry.GetHandler(message.Type);
                await handler.HandleAsync(message, cancellationToken);

                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAt = DateTime.UtcNow;
                message.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processing failed: {MessageId}", message.Id);

                message.Attempts++;
                message.ErrorMessage = ex.Message;

                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = OutboxMessageStatus.DeadLettered;
                    message.NextRetryAt = null;
                }
                else
                {
                    message.Status = OutboxMessageStatus.Pending;
                    message.NextRetryAt = DateTime.UtcNow.AddSeconds(
                        Math.Pow(2, message.Attempts) * 5);
                }
            }

            message.ProcessingStartedAt = null;
            message.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }

    private async Task RecoverStaleProcessing(
        ApplicationDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var cutoff = now - StaleProcessingThreshold;

        var stale = await db.OutboxMessages
            .Where(x =>
                x.Status == OutboxMessageStatus.Processing &&
                (x.ProcessingStartedAt == null || x.ProcessingStartedAt < cutoff))
            .ToListAsync(cancellationToken);

        foreach (var message in stale)
        {
            message.Status = OutboxMessageStatus.Pending;
            message.ProcessingStartedAt = null;
            message.UpdatedAt = now;
        }
    }
}
