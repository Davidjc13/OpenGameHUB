namespace OpenGameHUB.Services.Media;

public sealed record SystemMediaSnapshot(
    bool IsAvailable,
    string Title,
    string Artist,
    bool IsPlaying,
    bool CanToggle,
    bool CanNext,
    bool CanPrevious,
    string ArtworkKey,
    string SourceAppId = "")
{
    public static SystemMediaSnapshot None { get; } = new(
        false,
        string.Empty,
        string.Empty,
        false,
        false,
        false,
        false,
        string.Empty);
}
