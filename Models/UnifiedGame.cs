namespace OpenGameHUB.Models;

public sealed class UnifiedGame
{
    public required string Id { get; init; }
    public required Platform Platform { get; init; }
    public required string PlatformGameId { get; init; }
    public required string Title { get; init; }
    public bool IsInstalled { get; set; } = true;
    public string? InstallPath { get; set; }
    public string? CoverPath { get; set; }
    public int PlaytimeMinutes { get; set; }
    public DateTime? LastPlayed { get; set; }
    public bool IsFavorite { get; set; }
    public required LaunchSpec LaunchSpec { get; init; }
    public string PlatformLabel => PlatformLabels.Get(Platform);
}
