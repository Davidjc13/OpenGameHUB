using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure.Windows.Notifications;
using OpenGameHUB.Services.Notifications;

namespace OpenGameHUB.Tests;

public sealed class DiscordNotificationServiceTests
{
    [Fact]
    public void Receive_sets_banner_and_trims_ring_buffer_to_max_history()
    {
        using var service = CreateService();

        for (uint i = 1; i <= DiscordNotificationService.MaxHistory + 5; i++)
        {
            service.Receive(new DiscordNotificationMessage(
                i,
                $"User{i}",
                $"Body {i}",
                DateTimeOffset.UtcNow));
        }

        Assert.Equal(DiscordNotificationService.MaxHistory, service.Messages.Count);
        Assert.Equal(25u, service.Messages[0].Id);
        Assert.Equal(6u, service.Messages[^1].Id);
        Assert.Equal(25u, service.ActiveBanner?.Id);
    }

    [Fact]
    public void Deduplicates_by_id_and_preserves_existing_reply_draft()
    {
        using var service = CreateService();
        var now = DateTimeOffset.UtcNow;

        service.Receive(new DiscordNotificationMessage(10, "Alex", "Hola", now));
        service.OpenInbox(focusLatest: true);
        Assert.True(service.BeginReply());
        service.CommitReplyDraft("Voy en 5 min", "Guardado");

        service.Receive(new DiscordNotificationMessage(10, "Alex", "Hola actualizado", now.AddSeconds(2)));

        var single = Assert.Single(service.Messages);
        Assert.Equal("Hola actualizado", single.Body);
        Assert.Equal("Voy en 5 min", single.ReplyDraft);
    }

    [Fact]
    public void OpenInbox_clears_active_banner_and_unread_counter()
    {
        using var service = CreateService();

        service.Receive(new DiscordNotificationMessage(1, "Alex", "Uno", DateTimeOffset.UtcNow));
        service.Receive(new DiscordNotificationMessage(2, "Lucía", "Dos", DateTimeOffset.UtcNow));

        Assert.Equal(2, service.UnreadCount);
        Assert.NotNull(service.ActiveBanner);

        service.OpenInbox(focusLatest: true);

        Assert.True(service.InboxOpen);
        Assert.Null(service.ActiveBanner);
        Assert.Equal(0, service.UnreadCount);
        Assert.Equal(0, service.SelectedIndex);
    }

    private static DiscordNotificationService CreateService() =>
        new(
            source: new FakeDiscordNotificationSource(),
            bannerDuration: Timeout.InfiniteTimeSpan);

    private sealed class FakeDiscordNotificationSource : IDiscordNotificationSource
    {
#pragma warning disable CS0067
        public event EventHandler<DiscordNotificationMessage>? NotificationReceived;
#pragma warning restore CS0067

        public DiscordNotificationAccessState AccessState => DiscordNotificationAccessState.Allowed;

        public Task<DiscordNotificationAccessState> EnsureAccessAsync() =>
            Task.FromResult(DiscordNotificationAccessState.Allowed);

        public void OpenPrivacySettings()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
