using Windows.UI.Notifications.Management;

namespace OpenGameHUB.Infrastructure.Windows.Notifications;

public enum DiscordNotificationAccessState
{
    Unknown,
    Allowed,
    Denied,
    Unavailable
}

public interface IDiscordNotificationAccess
{
    DiscordNotificationAccessState GetStatus();

    Task<DiscordNotificationAccessState> RequestAccessAsync();

    void OpenPrivacySettings();
}

public sealed class DiscordNotificationAccess : IDiscordNotificationAccess
{
    private int _loggedFailure;

    public DiscordNotificationAccessState GetStatus()
    {
        if (!OperatingSystem.IsWindows())
            return DiscordNotificationAccessState.Unavailable;

        try
        {
            return Map(UserNotificationListener.Current.GetAccessStatus());
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(GetStatus));
            return DiscordNotificationAccessState.Unavailable;
        }
    }

    public async Task<DiscordNotificationAccessState> RequestAccessAsync()
    {
        if (!OperatingSystem.IsWindows())
            return DiscordNotificationAccessState.Unavailable;

        try
        {
            var current = UserNotificationListener.Current.GetAccessStatus();
            if (current == UserNotificationListenerAccessStatus.Allowed)
                return DiscordNotificationAccessState.Allowed;

            var requested = await UserNotificationListener.Current.RequestAccessAsync().AsTask();
            return Map(requested);
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(RequestAccessAsync));
            return DiscordNotificationAccessState.Unavailable;
        }
    }

    public void OpenPrivacySettings()
    {
        try
        {
            ProtocolLauncher.Start(ProtocolUri.WindowsNotificationPrivacySettings);
        }
        catch (Exception ex)
        {
            LogOnce(ex, nameof(OpenPrivacySettings));
        }
    }

    internal static DiscordNotificationAccessState Map(UserNotificationListenerAccessStatus status) =>
        status switch
        {
            UserNotificationListenerAccessStatus.Allowed => DiscordNotificationAccessState.Allowed,
            UserNotificationListenerAccessStatus.Denied => DiscordNotificationAccessState.Denied,
            UserNotificationListenerAccessStatus.Unspecified => DiscordNotificationAccessState.Unknown,
            _ => DiscordNotificationAccessState.Unavailable
        };

    private void LogOnce(Exception ex, string operation)
    {
        if (Interlocked.Exchange(ref _loggedFailure, 1) == 0)
        {
            AppDiagnostics.ReportError(
                area: nameof(DiscordNotificationAccess),
                operation: operation,
                exception: ex);
        }
    }
}
