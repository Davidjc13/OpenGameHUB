using OpenGameHUB.Services.Configuration;
using OpenGameHUB.Services.Media;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class SystemMediaControllerTests
{
    [Fact]
    public async Task Playback_commands_follow_the_system_session()
    {
        var media = new FakeSystemMediaController();
        var viewModel = new SystemMediaViewModel(media, static action => action());

        viewModel.Start();
        Assert.Equal(1, media.Starts);
        Assert.False(viewModel.ToggleCommand.CanExecute(null));

        media.Publish(new SystemMediaSnapshot(
            true,
            "Midnight City",
            "M83",
            true,
            true,
            true,
            false,
            "art"));

        Assert.True(viewModel.IsAvailable);
        Assert.Equal("Midnight City", viewModel.Title);
        Assert.Equal("M83", viewModel.Artist);
        Assert.True(viewModel.HasArtist);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal("\uE769", viewModel.ToggleGlyph);
        Assert.Equal(Loc.T("MediaPause"), viewModel.ToggleToolTip);
        Assert.True(viewModel.NextCommand.CanExecute(null));
        Assert.False(viewModel.PreviousCommand.CanExecute(null));

        await viewModel.ToggleCommand.ExecuteAsync(null);
        await viewModel.NextCommand.ExecuteAsync(null);

        Assert.Equal(1, media.Toggles);
        Assert.Equal(1, media.Nexts);
        Assert.Equal(0, media.Previous);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal("\uE768", viewModel.ToggleGlyph);

        media.Artwork = [1, 2, 3, 4];
        media.Publish(new SystemMediaSnapshot(
            true,
            "Midnight City",
            string.Empty,
            false,
            true,
            false,
            true,
            "cover"));

        Assert.False(viewModel.HasArtist);
        Assert.Null(viewModel.Artwork);
        Assert.False(viewModel.HasArtwork);

        viewModel.Stop();

        Assert.Equal(1, media.Stops);
        Assert.False(viewModel.IsAvailable);
        Assert.False(viewModel.ToggleCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData(@"C:\Apps\Spotify.exe", "Spotify")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Microsoft.ZuneMusic")]
    [InlineData("chrome", "chrome")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Source_name_drops_the_executable_and_package_suffix(string? sourceAppId, string expected)
    {
        Assert.Equal(expected, SystemMediaSourceNames.Format(sourceAppId));
    }

    [Fact]
    public void Spotify_window_title_stays_paused_until_the_track_changes()
    {
        var tracker = new SpotifyPlaybackTracker();

        var playing = tracker.Observe("ADÉLA - FinallyApologizing");
        Assert.True(playing.IsAvailable);
        Assert.True(playing.IsPlaying);
        Assert.Equal("FinallyApologizing", playing.Title);
        Assert.Equal("ADÉLA", playing.Artist);
        Assert.True(playing.CanNext);
        Assert.True(playing.CanPrevious);

        tracker.NoteToggle();
        var paused = tracker.Observe("ADÉLA - FinallyApologizing");
        Assert.False(paused.IsPlaying);

        tracker.NoteSkipped();
        var skipped = tracker.Observe("ADÉLA - FinallyApologizing");
        Assert.True(skipped.IsPlaying);

        var idle = tracker.Observe("Spotify Premium");
        Assert.True(idle.IsAvailable);
        Assert.False(idle.IsPlaying);
        Assert.Equal("Spotify", idle.Title);

        Assert.False(tracker.Observe("  ").IsAvailable);
    }

    private sealed class FakeSystemMediaController : ISystemMediaController
    {
        public SystemMediaSnapshot Current { get; private set; } = SystemMediaSnapshot.None;

        public event EventHandler? Changed;

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public int Toggles { get; private set; }

        public int Nexts { get; private set; }

        public int Previous { get; private set; }

        public byte[]? Artwork { get; set; }

        public void Publish(SystemMediaSnapshot snapshot)
        {
            Current = snapshot;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Start() => Starts++;

        public void Stop()
        {
            Stops++;
            Publish(SystemMediaSnapshot.None);
        }

        public ValueTask TogglePlayPauseAsync()
        {
            Toggles++;
            return ValueTask.CompletedTask;
        }

        public ValueTask NextAsync()
        {
            Nexts++;
            return ValueTask.CompletedTask;
        }

        public ValueTask PreviousAsync()
        {
            Previous++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]?> GetArtworkAsync() => ValueTask.FromResult(Artwork);
    }
}
