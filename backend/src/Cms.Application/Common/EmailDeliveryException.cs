namespace Cms.Application.Common;

/// <summary>
/// The external email provider could not accept a transactional message.
/// The message text is intentionally generic so provider details never reach clients.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(string message = "The email could not be delivered.")
        : base(message)
    {
    }
}
