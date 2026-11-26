using System.Collections.Concurrent;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Cms.Tests.Integration;

public sealed record VerificationEmailMessage(
    string Email,
    string DisplayName,
    string FromName,
    string Subject,
    string Html,
    string Text);

/// <summary>In-memory Resend replacement. No automated test sends real email.</summary>
public sealed class TestEmailVerificationSender : IEmailVerificationEmailSender
{
    private readonly object _gate = new();
    private readonly List<VerificationEmailMessage> _messages = [];

    public bool FailNext { get; set; }

    public IReadOnlyList<VerificationEmailMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public Task SendAsync(
        string recipientEmail,
        string displayName,
        string fromName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new EmailDeliveryException();
        }

        lock (_gate)
        {
            _messages.Add(new VerificationEmailMessage(
                recipientEmail,
                displayName,
                fromName,
                subject,
                htmlBody,
                textBody));
        }

        return Task.CompletedTask;
    }
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages;

    public CapturingLoggerProvider(ConcurrentQueue<string> messages)
    {
        _messages = messages;
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;

        public CapturingLogger(ConcurrentQueue<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                _messages.Enqueue(exception.ToString());
            }
        }
    }
}
