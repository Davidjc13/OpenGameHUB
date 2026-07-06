namespace OpenGameHUB.Models;

internal sealed class SettingsSecrets
{
    public string SteamApiKey { get; set; } = string.Empty;
    public string IgdbClientSecret { get; set; } = string.Empty;
    public string SteamGridDbApiKey { get; set; } = string.Empty;

    public void ApplyTo(AppSettings settings)
    {
        settings.SteamApiKey = SteamApiKey;
        settings.IgdbClientSecret = IgdbClientSecret;
        settings.SteamGridDbApiKey = SteamGridDbApiKey;
    }

    public static SettingsSecrets From(AppSettings settings) =>
        new()
        {
            SteamApiKey = settings.SteamApiKey,
            IgdbClientSecret = settings.IgdbClientSecret,
            SteamGridDbApiKey = settings.SteamGridDbApiKey
        };
}
