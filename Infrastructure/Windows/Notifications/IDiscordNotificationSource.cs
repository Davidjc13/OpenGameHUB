using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Infrastructure.Windows.Notifications;

public interface IDiscordNotificationSource : IDisposable
{
    event EventHandler<DiscordNotificationMessage>? NotificationReceived;

    DiscordNotificationAccessState AccessState { get; }

    Task<DiscordNotificationAccessState> EnsureAccessAsync();

    void OpenPrivacySettings();

    void Start();

    void Stop();
}
