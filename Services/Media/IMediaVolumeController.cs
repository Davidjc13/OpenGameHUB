namespace OpenGameHUB.Services.Media;

public readonly record struct MediaVolumeLevel(float Level, bool Muted);

public interface IMediaVolumeController
{
    bool TryGet(string processName, out MediaVolumeLevel level);

    bool TrySet(string processName, float level, bool muted);
}
