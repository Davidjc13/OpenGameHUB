namespace OpenGameHUB.Services.Media;

public interface ISystemMediaController
{
    SystemMediaSnapshot Current { get; }

    event EventHandler? Changed;

    void Start();

    void Stop();

    ValueTask TogglePlayPauseAsync();

    ValueTask NextAsync();

    ValueTask PreviousAsync();

    ValueTask<byte[]?> GetArtworkAsync();
}
