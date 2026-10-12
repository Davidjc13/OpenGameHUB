namespace OpenGameHUB.Services.Media;

public static class SpotifyPlayback
{
    public static SystemMediaSnapshot FromWindowTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return SystemMediaSnapshot.None;

        var trimmed = title.Trim();
        if (IsIdle(trimmed))
        {
            return new SystemMediaSnapshot(
                IsAvailable: true,
                Title: "Spotify",
                Artist: string.Empty,
                IsPlaying: false,
                CanToggle: true,
                CanNext: true,
                CanPrevious: true,
                ArtworkKey: string.Empty);
        }

        var split = trimmed.Split(" - ", 2, StringSplitOptions.TrimEntries);
        var artist = split.Length == 2 ? split[0] : "Spotify";
        var track = split.Length == 2 ? split[1] : trimmed;
        return new SystemMediaSnapshot(
            IsAvailable: true,
            Title: track,
            Artist: artist,
            IsPlaying: true,
            CanToggle: true,
            CanNext: true,
            CanPrevious: true,
            ArtworkKey: string.Empty);
    }

    private static bool IsIdle(string title) =>
        title.Equals("Spotify", StringComparison.OrdinalIgnoreCase)
        || title.Equals("Spotify Premium", StringComparison.OrdinalIgnoreCase)
        || title.Equals("Spotify Free", StringComparison.OrdinalIgnoreCase);
}

public sealed class SpotifyPlaybackTracker
{
    private string _title = string.Empty;
    private bool _paused;

    public SystemMediaSnapshot Observe(string? title)
    {
        var snapshot = SpotifyPlayback.FromWindowTitle(title);
        if (!snapshot.IsAvailable)
        {
            _title = string.Empty;
            _paused = false;
            return snapshot;
        }

        if (!string.Equals(title?.Trim(), _title, StringComparison.Ordinal))
        {
            _title = title?.Trim() ?? string.Empty;
            _paused = false;
        }

        return snapshot.IsPlaying
            ? snapshot with { IsPlaying = !_paused }
            : snapshot;
    }

    public void NoteToggle() => _paused = !_paused;

    public void NoteSkipped() => _paused = false;
}
