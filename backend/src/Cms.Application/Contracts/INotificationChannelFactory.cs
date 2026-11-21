namespace Cms.Application.Contracts;

/// <summary>
/// Factory for the M9 notification channels. A null or empty requested channel
/// means every supported channel; an unsupported value is rejected so a bad
/// payload fails loudly and is dead-lettered by the outbox processor.
/// </summary>
public interface INotificationChannelFactory
{
    IReadOnlyList<INotificationChannel> GetChannels(string? requestedChannel);
}
