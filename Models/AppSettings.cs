namespace OpenGameHUB.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = string.Empty;
    public string SteamApiKey { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string IgdbClientId { get; set; } = string.Empty;
    public string IgdbClientSecret { get; set; } = string.Empty;
    public string SteamGridDbApiKey { get; set; } = string.Empty;
    public bool ShowGridCovers { get; set; } = true;
    public bool DismissSteamApiKeyPrompt { get; set; }
    public bool DismissEaLibraryPrompt { get; set; }
    public bool DismissLegendaryPrompt { get; set; }

    public bool IsSteamApiConfigured =>
        !string.IsNullOrWhiteSpace(SteamApiKey) && !string.IsNullOrWhiteSpace(SteamId);

    public bool IsIgdbConfigured =>
        !string.IsNullOrWhiteSpace(IgdbClientId) && !string.IsNullOrWhiteSpace(IgdbClientSecret);

    public bool IsSteamGridDbConfigured => !string.IsNullOrWhiteSpace(SteamGridDbApiKey);

    public bool IsCoverMetadataConfigured => IsIgdbConfigured || IsSteamGridDbConfigured;
}
