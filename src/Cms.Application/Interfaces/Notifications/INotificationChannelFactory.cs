
using Cms.Application.Interfaces;

namespace Cms.Application.Interfaces.Notifications;

public interface INotificationChannelFactory
{
    INotificationChannel GetChannel(string channel);
}
