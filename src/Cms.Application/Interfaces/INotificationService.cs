
namespace Cms.Application.Interfaces;

public interface INotificationService
{
    Task SendAsync(
        Guid userId,
        string title,
        string message,
        string channel,
        CancellationToken cancellationToken = default);
}
