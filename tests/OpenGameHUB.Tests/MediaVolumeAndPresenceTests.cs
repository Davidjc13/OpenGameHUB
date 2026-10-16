using System.Diagnostics;
using System.Text.Json;
using OpenGameHUB.Infrastructure.Media;
using OpenGameHUB.Services.Media;
using OpenGameHUB.Services.Presence;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class MediaVolumeAndPresenceTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData(@"C:\Apps\Spotify.exe", "Spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("Discord.exe", null)]
    [InlineData("DiscordPTB.exe", null)]
    [InlineData("", null)]
    public void Volume_follows_the_music_app_and_skips_discord_voice(string? sourceAppId, string? expected)
    {
        Assert.Equal(expected, MediaVolumeTarget.ProcessName(sourceAppId));
    }

    [Theory]
    [InlineData("Spotify", "Spotify", true)]
    [InlineData("SpotifyAB.SpotifyMusic", "Spotify", true)]
    [InlineData("Microsoft.ZuneMusic", "Music", true)]
    [InlineData("Spotify", "Discord", false)]
    public void Volume_process_names_match_running_sessions(string target, string actual, bool expected)
    {
        Assert.Equal(expected, MediaVolumeTarget.MatchesProcess(target, actual));
    }

    [Fact]
    public async Task Slider_sets_the_music_process_volume()
    {
        var media = new FakeSystemMediaController();
        var volume = new FakeMediaVolume { Level = new MediaVolumeLevel(0.4f, false) };
        var viewModel = new SystemMediaViewModel(media, static action => action(), volume);

        viewModel.Start();
        media.Publish(new SystemMediaSnapshot(
            true,
            "Midnight City",
            "M83",
            true,
            true,
            true,
            true,
            "art",
            "Spotify.exe"));

        Assert.True(viewModel.CanControlVolume);
        Assert.Equal(40, viewModel.Volume);
        Assert.Equal("\uE767", viewModel.VolumeGlyph);

        viewModel.Volume = 0;
        await Task.Yield();

        Assert.Equal("Spotify", volume.ProcessName);
        Assert.Equal(0f, volume.Level.Level);
        Assert.True(volume.Level.Muted);
        Assert.Equal("\uE74F", viewModel.VolumeGlyph);

        media.Publish(new SystemMediaSnapshot(
            true,
            "Midnight City",
            "M83",
            true,
            true,
            true,
            true,
            "art",
            "Discord.exe"));

        Assert.False(viewModel.CanControlVolume);
    }

    [Fact]
    public async Task Mute_restores_the_previous_level()
    {
        var media = new FakeSystemMediaController();
        var volume = new FakeMediaVolume { Level = new MediaVolumeLevel(0.4f, false) };
        var viewModel = CreateVolumeViewModel(media, volume);

        viewModel.Start();
        PublishSpotify(media);

        viewModel.ToggleMute();
        await Task.Yield();

        Assert.True(viewModel.IsMuted);
        Assert.True(volume.Level.Muted);
        Assert.Equal(40, viewModel.Volume);
        Assert.True(viewModel.OsdVisible);
        Assert.True(viewModel.OsdMuted);

        viewModel.ToggleMute();
        await Task.Yield();

        Assert.False(viewModel.IsMuted);
        Assert.False(volume.Level.Muted);
        Assert.Equal(0.4f, volume.Level.Level);
        Assert.Equal(40, viewModel.Volume);
    }

    [Fact]
    public async Task Volume_bump_clamps_and_unmutes()
    {
        var media = new FakeSystemMediaController();
        var volume = new FakeMediaVolume { Level = new MediaVolumeLevel(0.04f, false) };
        var viewModel = CreateVolumeViewModel(media, volume);

        viewModel.Start();
        PublishSpotify(media);

        viewModel.BumpVolume(-10);
        await Task.Yield();

        Assert.Equal(0, viewModel.Volume);
        Assert.True(viewModel.IsMuted);

        viewModel.ToggleMute();
        viewModel.BumpVolume(SystemMediaViewModel.VolumeStep);
        await Task.Yield();

        Assert.False(viewModel.IsMuted);
        Assert.Equal(9, viewModel.Volume);

        viewModel.Volume = 98;
        viewModel.BumpVolume(SystemMediaViewModel.VolumeStep);
        await Task.Yield();

        Assert.Equal(100, viewModel.Volume);
    }

    [Fact]
    public void Unavailable_volume_still_shows_the_osd()
    {
        var media = new FakeSystemMediaController();
        var viewModel = CreateVolumeViewModel(media, new FakeMediaVolume());

        viewModel.Start();
        viewModel.BumpVolume(SystemMediaViewModel.VolumeStep);

        Assert.False(viewModel.CanControlVolume);
        Assert.True(viewModel.OsdVisible);
        Assert.Equal(100, viewModel.OsdVolume);
    }

    [Fact]
    public void Empty_source_still_reads_device_volume()
    {
        var media = new FakeSystemMediaController();
        var volume = new FakeMediaVolume
        {
            Level = new MediaVolumeLevel(0.7f, false),
            AllowDeviceFallback = true
        };
        var viewModel = CreateVolumeViewModel(media, volume);

        viewModel.Start();
        media.Publish(new SystemMediaSnapshot(
            true,
            "Song",
            "Artist",
            true,
            true,
            true,
            true,
            "art",
            ""));

        Assert.True(viewModel.CanControlVolume);
        Assert.Equal(70, viewModel.Volume);
    }

    private static SystemMediaViewModel CreateVolumeViewModel(
        FakeSystemMediaController media,
        FakeMediaVolume volume) =>
        new(media, static action => action(), volume, static (_, token) => Task.Delay(Timeout.Infinite, token));

    private static void PublishSpotify(FakeSystemMediaController media) =>
        media.Publish(new SystemMediaSnapshot(
            true,
            "Midnight City",
            "M83",
            true,
            true,
            true,
            true,
            "art",
            "Spotify.exe"));

    [Fact]
    public void Reading_session_volume_does_not_throw()
    {
        var volume = new WindowsMediaVolume();

        volume.TryGet("OpenGameHUB-missing-player", out _);
        volume.TryGet(Process.GetCurrentProcess().ProcessName, out _);
        volume.TryGet(null, out _);
    }

    [Fact]
    public void Default_device_volume_can_be_read()
    {
        var volume = new WindowsMediaVolume();
        Assert.True(volume.TryGet(null, out var level));
        Assert.InRange(level.Level, 0f, 1f);
    }

    [Fact]
    public void Discord_activity_names_the_selected_game()
    {
        Assert.False(DiscordPresenceFrames.IsApplicationId(""));
        Assert.False(DiscordPresenceFrames.IsApplicationId("not-an-id"));
        Assert.True(DiscordPresenceFrames.IsApplicationId("12345678901234567"));

        var json = DiscordPresenceFrames.SetActivity(
            42,
            "nonce",
            new DiscordActivity("Hades", "Steam"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("SET_ACTIVITY", root.GetProperty("cmd").GetString());
        Assert.Equal(42, root.GetProperty("args").GetProperty("pid").GetInt32());
        Assert.Equal("Hades", root.GetProperty("args").GetProperty("activity").GetProperty("details").GetString());
        Assert.Equal("Steam", root.GetProperty("args").GetProperty("activity").GetProperty("state").GetString());

        var cleared = DiscordPresenceFrames.ClearActivity(42, "nonce");
        using var clearDocument = JsonDocument.Parse(cleared);
        Assert.Equal(JsonValueKind.Null, clearDocument.RootElement.GetProperty("args").GetProperty("activity").ValueKind);
    }

    private sealed class FakeMediaVolume : IMediaVolumeController
    {
        public MediaVolumeLevel Level { get; set; }

        public string? ProcessName { get; private set; }

        public bool AllowDeviceFallback { get; set; }

        public bool TryGet(string? processName, out MediaVolumeLevel level)
        {
            ProcessName = processName;
            if (string.IsNullOrWhiteSpace(processName) && !AllowDeviceFallback)
            {
                level = default;
                return false;
            }

            level = Level;
            return true;
        }

        public bool TrySet(string? processName, float level, bool muted)
        {
            ProcessName = processName;
            Level = new MediaVolumeLevel(level, muted);
            return true;
        }
    }

    private sealed class FakeSystemMediaController : ISystemMediaController
    {
        public SystemMediaSnapshot Current { get; private set; } = SystemMediaSnapshot.None;

        public event EventHandler? Changed;

        public void Publish(SystemMediaSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Start()
        {
        }

        public void Stop() => Publish(SystemMediaSnapshot.None);

        public ValueTask TogglePlayPauseAsync() => ValueTask.CompletedTask;

        public ValueTask NextAsync() => ValueTask.CompletedTask;

        public ValueTask PreviousAsync() => ValueTask.CompletedTask;

        public ValueTask<byte[]?> GetArtworkAsync() => ValueTask.FromResult<byte[]?>(null);
    }
}
