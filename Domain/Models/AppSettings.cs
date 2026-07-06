namespace OpenGameHUB.Domain.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = string.Empty;
    public string SteamApiKey { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string IgdbClientId { get; set; } = string.Empty;
    public string IgdbClientSecret { get; set; } = string.Empty;
    public string SteamGridDbApiKey { get; set; } = string.Empty;
    public CoverQualityMode CoverQualityMode { get; set; } = CoverQualityMode.Low;
    public LibraryViewMode LibraryViewMode { get; set; } = LibraryViewMode.Grid;
    public bool DismissSteamApiKeyPrompt { get; set; }
    public bool DismissEaLibraryPrompt { get; set; }
    public bool DismissLegendaryPrompt { get; set; }
    public string EpicAccountId { get; set; } = string.Empty;
    public string EpicDisplayName { get; set; } = string.Empty;
    public string XboxGamertag { get; set; } = string.Empty;

    public bool HasEpicAuth => !string.IsNullOrWhiteSpace(EpicAccountId);

    public bool IsSteamApiConfigured =>
        !string.IsNullOrWhiteSpace(SteamApiKey) && !string.IsNullOrWhiteSpace(SteamId);

    public bool IsIgdbConfigured =>
        !string.IsNullOrWhiteSpace(IgdbClientId) && !string.IsNullOrWhiteSpace(IgdbClientSecret);

    public bool IsSteamGridDbConfigured => !string.IsNullOrWhiteSpace(SteamGridDbApiKey);

    public bool IsCoverMetadataConfigured => IsIgdbConfigured || IsSteamGridDbConfigured;

    public AppSettings Clone() =>
        new()
        {
            Language = Language,
            SteamApiKey = SteamApiKey,
            SteamId = SteamId,
            IgdbClientId = IgdbClientId,
            IgdbClientSecret = IgdbClientSecret,
            SteamGridDbApiKey = SteamGridDbApiKey,
            CoverQualityMode = CoverQualityMode,
            LibraryViewMode = LibraryViewMode,
            DismissSteamApiKeyPrompt = DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = DismissEaLibraryPrompt,
            DismissLegendaryPrompt = DismissLegendaryPrompt,
            EpicAccountId = EpicAccountId,
            EpicDisplayName = EpicDisplayName,
            XboxGamertag = XboxGamertag
        };
}
