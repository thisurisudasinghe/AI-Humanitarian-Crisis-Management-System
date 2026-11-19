using Cms.Application.Contracts;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Infrastructure;

public class OutboxProcessorTests
{
    [Fact]
    public async Task Processor_dispatches_a_pending_message_and_marks_it_processed()
    {
        var handler = new RecordingHandler("Request.Submitted");
        using var harness = new ProcessorHarness(handler);

        var id = await harness.SeedAsync(OutboxMessageStatus.Pending);

        var processed = await harness.Processor.ProcessOnceAsync();

        Assert.Equal(1, processed);
        Assert.Equal(1, handler.HandledCount);

        var message = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(0, message.RetryCount);
        Assert.Null(message.ErrorMessage);
        Assert.Null(message.NextRetryAt);
    }

    [Fact]
    public async Task Processor_retries_and_then_dead_letters_a_failing_message()
    {
        var handler = new RecordingHandler("Request.Submitted", new InvalidOperationException("boom"));
        using var harness = new ProcessorHarness(handler, new Dictionary<string, string?>
        {
            ["Outbox:MaxRetries"] = "3",
            ["Outbox:RetryDelaySeconds"] = "0"
        });

        var id = await harness.SeedAsync(OutboxMessageStatus.Pending);

        await harness.Processor.ProcessOnceAsync();
        var afterFirst = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.Failed, afterFirst.Status);
        Assert.Equal(1, afterFirst.RetryCount);
        Assert.Contains("boom", afterFirst.ErrorMessage);

        await harness.Processor.ProcessOnceAsync();
        var afterSecond = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.Failed, afterSecond.Status);
        Assert.Equal(2, afterSecond.RetryCount);

        await harness.Processor.ProcessOnceAsync();
        var afterThird = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.DeadLettered, afterThird.Status);
        Assert.Equal(3, afterThird.RetryCount);
        Assert.Null(afterThird.NextRetryAt);
        Assert.Equal(3, handler.HandledCount);
    }

    [Fact]
    public async Task Processor_marks_a_message_failed_when_no_handler_is_registered()
    {
        using var harness = new ProcessorHarness();

        var id = await harness.SeedAsync(OutboxMessageStatus.Pending);

        var processed = await harness.Processor.ProcessOnceAsync();

        Assert.Equal(0, processed);
        var message = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("No outbox handler", message.ErrorMessage);
    }

    [Fact]
    public async Task Processor_recovers_stale_processing_messages()
    {
        var handler = new RecordingHandler("Request.Submitted");
        using var harness = new ProcessorHarness(handler, new Dictionary<string, string?>
        {
            ["Outbox:StaleProcessingMinutes"] = "1",
            ["Outbox:RetryDelaySeconds"] = "0"
        });

        var id = await harness.SeedAsync(
            OutboxMessageStatus.Processing,
            updatedAt: DateTime.UtcNow.AddHours(-1));

        var processed = await harness.Processor.ProcessOnceAsync();

        Assert.Equal(1, processed);
        Assert.Equal(1, handler.HandledCount);

        var message = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        Assert.Equal(1, message.RetryCount);
    }

    [Fact]
    public async Task Processor_ignores_messages_whose_retry_is_in_the_future()
    {
        var handler = new RecordingHandler("Request.Submitted");
        using var harness = new ProcessorHarness(handler);

        var id = await harness.SeedAsync(
            OutboxMessageStatus.Failed,
            nextRetryAt: DateTime.UtcNow.AddHours(1));

        var processed = await harness.Processor.ProcessOnceAsync();

        Assert.Equal(0, processed);
        Assert.Equal(0, handler.HandledCount);

        var message = await harness.GetMessageAsync(id);
        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
    }

    private sealed class ProcessorHarness : IDisposable
    {
        private readonly string _databaseName = $"cms-outbox-processor-{Guid.NewGuid()}";
        private readonly ServiceProvider _provider;

        public ProcessorHarness(
            IOutboxHandler? handler = null,
            IDictionary<string, string?>? settings = null)
        {
            var configuration = new Dictionary<string, string?>
            {
                ["Outbox:ProcessorEnabled"] = "true",
                ["Outbox:BatchSize"] = "20",
                ["Outbox:MaxRetries"] = "3",
                ["Outbox:RetryDelaySeconds"] = "0",
                ["Outbox:StaleProcessingMinutes"] = "10"
            };

            if (settings is not null)
            {
                foreach (var pair in settings)
                {
                    configuration[pair.Key] = pair.Value;
                }
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(
                new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
            services.AddDbContext<CmsDbContext>(
                options => options.UseInMemoryDatabase(_databaseName));

            if (handler is not null)
            {
                services.AddSingleton<IOutboxHandler>(handler);
            }

            services.AddSingleton<OutboxProcessor>();

            _provider = services.BuildServiceProvider();
            Processor = _provider.GetRequiredService<OutboxProcessor>();
        }

        public OutboxProcessor Processor { get; }

        public async Task<Guid> SeedAsync(
            OutboxMessageStatus status,
            DateTime? nextRetryAt = null,
            DateTime? updatedAt = null)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            var message = new OutboxMessage
            {
                Type = "Request.Submitted",
                EntityId = Guid.NewGuid(),
                Payload = "{}",
                IdempotencyKey = $"key:{Guid.NewGuid():N}",
                Status = status,
                RetryCount = 0,
                NextRetryAt = nextRetryAt,
                UpdatedAt = updatedAt
            };

            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();

            return message.Id;
        }

        public async Task<OutboxMessage> GetMessageAsync(Guid id)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            return await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == id);
        }

        public void Dispose() => _provider.Dispose();
    }

    private sealed class RecordingHandler : IOutboxHandler
    {
        private readonly Exception? _exception;

        public RecordingHandler(string type, Exception? exception = null)
        {
            Type = type;
            _exception = exception;
        }

        public string Type { get; }

        public int HandledCount { get; private set; }

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            HandledCount++;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.CompletedTask;
        }
    }
}
