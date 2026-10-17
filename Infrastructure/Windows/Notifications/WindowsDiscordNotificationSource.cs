using System.Runtime.InteropServices;
using OpenGameHUB.Domain.Models;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace OpenGameHUB.Infrastructure.Windows.Notifications;

public sealed class WindowsDiscordNotificationSource : IDiscordNotificationSource
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly IDiscordNotificationAccess _access;
    private readonly Func<bool> _suppressSystemToasts;
    private readonly TimeSpan _pollInterval;
    private readonly HashSet<uint> _seenIds = [];
    private readonly object _gate = new();
    private SynchronizationContext? _ui;
    private Timer? _timer;
    private int _active;
    private int _polling;
    private bool _primed;
    private int _loggedFailure;

    public WindowsDiscordNotificationSource(
        IDiscordNotificationAccess? access = null,
        Func<bool>? suppressSystemToasts = null,
        TimeSpan? pollInterval = null)
    {
        _access = access ?? new DiscordNotificationAccess();
        _suppressSystemToasts = suppressSystemToasts ?? (() => true);
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public event EventHandler<DiscordNotificationMessage>? NotificationReceived;

    public DiscordNotificationAccessState AccessState { get; private set; } = DiscordNotificationAccessState.Unknown;

    public async Task<DiscordNotificationAccessState> EnsureAccessAsync()
    {
        var state = await _access.RequestAccessAsync();
        AccessState = state;
        return state;
    }

    public void OpenPrivacySettings() => _access.OpenPrivacySettings();

    public void Start()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return;

        _ui = SynchronizationContext.Current;
        AccessState = _access.GetStatus();
        _timer = new Timer(OnTick, null, TimeSpan.Zero, _pollInterval);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _active, 0) == 0)
            return;

        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private void OnTick(object? state)
    {
        if (Volatile.Read(ref _active) == 0)
            return;

        if (Interlocked.CompareExchange(ref _polling, 1, 0) != 0)
            return;

        _ = PollOnceAsync();
    }

    private async Task PollOnceAsync()
    {
        try
        {
            if (!OperatingSystem.IsWindows() || Volatile.Read(ref _active) == 0)
                return;

            var status = _access.GetStatus();
            AccessState = status;
            if (status != DiscordNotificationAccessState.Allowed)
                return;

            var listener = UserNotificationListener.Current;
            var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask().ConfigureAwait(false);
            if (Volatile.Read(ref _active) == 0 || notifications is null)
                return;

            var discovered = new List<DiscordNotificationMessage>();
            lock (_gate)
            {
                var currentIds = new HashSet<uint>();
                foreach (var item in notifications)
                {
                    currentIds.Add(item.Id);
                    if (_seenIds.Contains(item.Id))
                        continue;

                    _seenIds.Add(item.Id);
                    if (!IsDiscordNotification(item))
                        continue;

                    if (!_primed)
                        continue;

                    if (TryExtractMessage(item, out var parsed) && parsed is not null)
                    {
                        discovered.Add(parsed);
                        if (_suppressSystemToasts())
                            TryRemoveSystemToast(listener, item.Id);
                    }
                }

                _seenIds.IntersectWith(currentIds);
                _primed = true;
            }

            foreach (var msg in discovered)
                Publish(msg);
        }
        catch (Exception ex) when (IsNotificationFailure(ex))
        {
            LogOnce(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private static bool IsDiscordNotification(UserNotification notification)
    {
        string? aumid = null;
        string? displayName = null;
        try
        {
            aumid = notification.AppInfo?.AppUserModelId;
            displayName = notification.AppInfo?.DisplayInfo?.DisplayName;
        }
        catch (Exception)
        {
        }

        return DiscordToastParser.IsDiscordApp(aumid, displayName);
    }

    private static bool TryExtractMessage(UserNotification item, out DiscordNotificationMessage? message)
    {
        message = null;
        try
        {
            var visual = item.Notification?.Visual;
            if (visual is null)
                return false;

            var texts = new List<string?>();
            var generic = visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (generic is not null)
            {
                foreach (var element in generic.GetTextElements())
                    texts.Add(element.Text);
            }
            else
            {
                foreach (var binding in visual.Bindings)
                {
                    foreach (var element in binding.GetTextElements())
                        texts.Add(element.Text);
                }
            }

            return DiscordToastParser.TryParse(item.Id, item.CreationTime, texts, out message);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryRemoveSystemToast(UserNotificationListener listener, uint id)
    {
        try
        {
            listener.RemoveNotification(id);
        }
        catch (Exception)
        {
        }
    }

    private void Publish(DiscordNotificationMessage message)
    {
        var ui = _ui;
        if (ui is null || SynchronizationContext.Current == ui)
            NotificationReceived?.Invoke(this, message);
        else
            ui.Post(_ => NotificationReceived?.Invoke(this, message), null);
    }

    private void LogOnce(Exception ex)
    {
        if (Interlocked.Exchange(ref _loggedFailure, 1) == 0)
        {
            AppDiagnostics.ReportError(
                area: nameof(WindowsDiscordNotificationSource),
                operation: nameof(PollOnceAsync),
                exception: ex);
        }
    }

    private static bool IsNotificationFailure(Exception ex) =>
        ex is COMException or InvalidOperationException or UnauthorizedAccessException
            or ArgumentException or ObjectDisposedException;
}
