namespace OpenGameHUB.Services.Auth;

internal sealed class SteamBrowserCaptureResult
{
    public string? ApiKey { get; init; }

    public string? SteamId { get; init; }

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SteamId);
}
