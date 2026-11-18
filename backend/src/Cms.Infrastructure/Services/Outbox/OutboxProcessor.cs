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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var handler = scope.ServiceProvider
                    .GetRequiredService<IOutboxHandler>();

                var staleProcessingMessages = await db.OutboxMessages
                    .Where(x =>
                    x.Status == OutboxMessageStatus.Processing &&
                   (x.ProcessingStartedAt == null ||
                    x.ProcessingStartedAt < DateTime.UtcNow.AddMinutes(-5)))
                    .ToListAsync(stoppingToken);

                foreach (var staleMessage in staleProcessingMessages)
                {
                    staleMessage.Status = OutboxMessageStatus.Pending;
                    staleMessage.ProcessingStartedAt = null;
                }

                await db.SaveChangesAsync(stoppingToken);

                var messages = await db.OutboxMessages
                    .Where(x =>
                        x.Status == OutboxMessageStatus.Pending &&
                        (x.NextRetryAt == null ||
                         x.NextRetryAt <= DateTime.UtcNow))
                    .OrderBy(x => x.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        message.Status = OutboxMessageStatus.Processing;
                        message.ProcessingStartedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);

                        await handler.HandleAsync(message, stoppingToken);

                        message.Status = OutboxMessageStatus.Processed;
                        message.ProcessedAt = DateTime.UtcNow;
                        message.ErrorMessage = null;
                        message.ProcessingStartedAt = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Outbox processing failed: {MessageId}",
                            message.Id);

                        message.Attempts++;
                        message.ErrorMessage = ex.Message;

                        if (message.Attempts >= 3)
                        {
                            message.Status = OutboxMessageStatus.DeadLettered;
                        }
                        else
                        {
                            message.Status = OutboxMessageStatus.Pending;
                            message.NextRetryAt = DateTime.UtcNow.AddSeconds(
                                Math.Pow(2, message.Attempts) * 5);
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox processor error.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}