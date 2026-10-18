using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Windows.Notifications;

namespace OpenGameHUB.Services.Notifications;

public sealed class DiscordNotificationService : IDisposable
{
    public const int MaxHistory = 20;
    public static readonly TimeSpan DefaultBannerDuration = TimeSpan.FromSeconds(5);

    private readonly IDiscordNotificationSource _source;
    private readonly Func<bool> _isEnabled;
    private readonly TimeSpan _bannerDuration;
    private readonly List<DiscordNotificationMessage> _messages = [];
    private readonly SynchronizationContext? _ui;
    private Timer? _bannerTimer;
    private bool _active;
    private bool _requestedAccessOnce;
    private int _bannerGeneration;

    public DiscordNotificationService(
        IDiscordNotificationSource? source = null,
        Func<bool>? isEnabled = null,
        Func<bool>? suppressSystemToasts = null,
        TimeSpan? bannerDuration = null)
    {
        _source = source ?? new WindowsDiscordNotificationSource(suppressSystemToasts: suppressSystemToasts);
        _isEnabled = isEnabled ?? (() => true);
        _bannerDuration = bannerDuration ?? DefaultBannerDuration;
        _ui = SynchronizationContext.Current;
        _source.NotificationReceived += OnSourceNotificationReceived;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<DiscordNotificationMessage> Messages => _messages;

    public DiscordNotificationMessage? ActiveBanner { get; private set; }

    public int UnreadCount { get; private set; }

    public bool InboxOpen { get; private set; }

    public int SelectedIndex { get; private set; }

    public DiscordNotificationMessage? SelectedMessage =>
        SelectedIndex >= 0 && SelectedIndex < _messages.Count
            ? _messages[SelectedIndex]
            : null;

    public bool Replying { get; private set; }

    public string ReplyDraft { get; private set; } = string.Empty;

    public string? StatusFeedback { get; private set; }

    public DiscordNotificationAccessState AccessState { get; private set; } = DiscordNotificationAccessState.Unknown;

    public bool IsPermissionDenied => AccessState == DiscordNotificationAccessState.Denied;

    public void Activate()
    {
        if (_active)
            return;

        _active = true;
        if (!_isEnabled())
        {
            RaiseChanged();
            return;
        }

        _ = ActivateCoreAsync();
    }

    public void Deactivate()
    {
        if (!_active)
            return;

        _active = false;
        StopBannerTimer();
        _source.Stop();
        ActiveBanner = null;
        InboxOpen = false;
        Replying = false;
        ReplyDraft = string.Empty;
        StatusFeedback = null;
        RaiseChanged();
    }

    public void RefreshEnabledState()
    {
        if (!_active)
            return;

        if (_isEnabled())
        {
            _ = ActivateCoreAsync();
        }
        else
        {
            StopBannerTimer();
            _source.Stop();
            ActiveBanner = null;
            InboxOpen = false;
            Replying = false;
            StatusFeedback = null;
            RaiseChanged();
        }
    }

    public void Receive(DiscordNotificationMessage message)
    {
        if (!_isEnabled())
            return;

        var existingIndex = _messages.FindIndex(m => m.Id == message.Id);
        if (existingIndex >= 0)
        {
            var preservedDraft = _messages[existingIndex].ReplyDraft;
            _messages.RemoveAt(existingIndex);
            message = message with { ReplyDraft = preservedDraft };
        }

        _messages.Insert(0, message);
        while (_messages.Count > MaxHistory)
            _messages.RemoveAt(_messages.Count - 1);

        SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _messages.Count - 1));

        if (!InboxOpen)
        {
            ActiveBanner = message;
            UnreadCount++;
            ScheduleBannerDismiss();
        }

        RaiseChanged();
    }

    public void DismissBanner()
    {
        if (ActiveBanner is null)
            return;

        StopBannerTimer();
        ActiveBanner = null;
        RaiseChanged();
    }

    public void OpenInbox(bool focusLatest = false)
    {
        StopBannerTimer();
        ActiveBanner = null;
        InboxOpen = true;
        Replying = false;
        UnreadCount = 0;
        StatusFeedback = null;
        if (focusLatest || SelectedIndex < 0 || SelectedIndex >= _messages.Count)
            SelectedIndex = 0;

        RaiseChanged();
    }

    public void CloseInbox()
    {
        if (!InboxOpen && !Replying)
            return;

        Replying = false;
        InboxOpen = false;
        ReplyDraft = string.Empty;
        StatusFeedback = null;
        RaiseChanged();
    }

    public void ToggleInbox()
    {
        if (InboxOpen)
            CloseInbox();
        else
            OpenInbox(focusLatest: ActiveBanner is not null);
    }

    public void MoveSelection(int delta)
    {
        if (!InboxOpen || Replying || _messages.Count == 0 || delta == 0)
            return;

        var next = Math.Clamp(SelectedIndex + delta, 0, _messages.Count - 1);
        if (next == SelectedIndex)
            return;

        SelectedIndex = next;
        StatusFeedback = null;
        RaiseChanged();
    }

    public void SelectAndReply(uint id)
    {
        var idx = _messages.FindIndex(m => m.Id == id);
        if (idx < 0)
            return;

        SelectedIndex = idx;
        BeginReply();
    }

    public bool BeginReply()
    {
        if (!InboxOpen || SelectedMessage is not { } target)
            return false;

        Replying = true;
        ReplyDraft = target.ReplyDraft ?? string.Empty;
        StatusFeedback = null;
        RaiseChanged();
        return true;
    }

    public void UpdateReplyDraft(string text)
    {
        if (!Replying || ReplyDraft == text)
            return;

        ReplyDraft = text;
        RaiseChanged();
    }

    public void CancelReply()
    {
        if (!Replying)
            return;

        Replying = false;
        ReplyDraft = string.Empty;
        RaiseChanged();
    }

    public void CommitReplyDraft(string? text = null, string? savedNotice = null)
    {
        if (SelectedIndex < 0 || SelectedIndex >= _messages.Count)
            return;

        var draft = (text ?? ReplyDraft).Trim();
        var updated = _messages[SelectedIndex] with
        {
            ReplyDraft = string.IsNullOrEmpty(draft) ? null : draft
        };
        _messages[SelectedIndex] = updated;
        Replying = false;
        ReplyDraft = string.Empty;
        StatusFeedback = string.IsNullOrEmpty(draft) ? null : savedNotice;
        RaiseChanged();
    }

    public void OpenPrivacySettings() => _source.OpenPrivacySettings();

    public void Dispose()
    {
        StopBannerTimer();
        _source.NotificationReceived -= OnSourceNotificationReceived;
        _source.Dispose();
    }

    private async Task ActivateCoreAsync()
    {
        if (!_requestedAccessOnce)
        {
            _requestedAccessOnce = true;
            AccessState = await _source.EnsureAccessAsync();
        }
        else
        {
            AccessState = _source.AccessState;
        }

        if (!_active || !_isEnabled())
            return;

        if (AccessState == DiscordNotificationAccessState.Allowed)
            _source.Start();

        RaiseChanged();
    }

    private void OnSourceNotificationReceived(object? sender, DiscordNotificationMessage e)
    {
        if (!_active || !_isEnabled())
            return;

        Receive(e);
    }

    private void ScheduleBannerDismiss()
    {
        StopBannerTimer();
        if (_bannerDuration <= TimeSpan.Zero || _bannerDuration == Timeout.InfiniteTimeSpan)
            return;

        var gen = Interlocked.Increment(ref _bannerGeneration);
        _bannerTimer = new Timer(_ => OnBannerTimerExpired(gen), null, _bannerDuration, Timeout.InfiniteTimeSpan);
    }

    private void OnBannerTimerExpired(int gen)
    {
        if (gen != Volatile.Read(ref _bannerGeneration))
            return;

        if (_ui is null || SynchronizationContext.Current == _ui)
            DismissBanner();
        else
            _ui.Post(_ => DismissBanner(), null);
    }

    private void StopBannerTimer()
    {
        Interlocked.Increment(ref _bannerGeneration);
        _bannerTimer?.Dispose();
        _bannerTimer = null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
