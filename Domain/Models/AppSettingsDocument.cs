namespace OpenGameHUB.Domain.Models;

/// <summary>
/// Serializable preferences written to settings.json (no API secrets).
/// </summary>
internal sealed class AppSettingsDocument
{
    public string Language { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string IgdbClientId { get; set; } = string.Empty;
    public CoverQualityMode CoverQualityMode { get; set; } = CoverQualityMode.Low;
    public UiFontScale UiFontScale { get; set; } = UiFontScale.Normal;
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
    public LibraryViewMode LibraryViewMode { get; set; } = LibraryViewMode.Grid;
    public double GridCardSize { get; set; } = 224;
    public bool PageSlideEnabled { get; set; }
    public bool RefreshLibraryOnStartup { get; set; }
    public bool DismissSteamApiKeyPrompt { get; set; }
    public bool DismissEaLibraryPrompt { get; set; }
    public bool DismissLegendaryPrompt { get; set; }
    public string EpicAccountId { get; set; } = string.Empty;
    public string EpicDisplayName { get; set; } = string.Empty;
    public string XboxGamertag { get; set; } = string.Empty;
    public string BattleNetDisplayName { get; set; } = string.Empty;
    public string DiscordApplicationId { get; set; } = string.Empty;
    public bool DiscordConsoleNotificationsEnabled { get; set; } = true;
    public bool SuppressSystemDiscordToasts { get; set; } = true;

    public static AppSettingsDocument From(AppSettings settings) =>
        new()
        {
            Language = settings.Language,
            SteamId = settings.SteamId,
            IgdbClientId = settings.IgdbClientId,
            CoverQualityMode = settings.CoverQualityMode,
            UiFontScale = settings.UiFontScale,
            ThemeMode = settings.ThemeMode,
            LibraryViewMode = settings.LibraryViewMode,
            GridCardSize = settings.GridCardSize,
            PageSlideEnabled = settings.PageSlideEnabled,
            RefreshLibraryOnStartup = settings.RefreshLibraryOnStartup,
            DismissSteamApiKeyPrompt = settings.DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = settings.DismissEaLibraryPrompt,
            DismissLegendaryPrompt = settings.DismissLegendaryPrompt,
            EpicAccountId = settings.EpicAccountId,
            EpicDisplayName = settings.EpicDisplayName,
            XboxGamertag = settings.XboxGamertag,
            BattleNetDisplayName = settings.BattleNetDisplayName,
            DiscordApplicationId = settings.DiscordApplicationId,
            DiscordConsoleNotificationsEnabled = settings.DiscordConsoleNotificationsEnabled,
            SuppressSystemDiscordToasts = settings.SuppressSystemDiscordToasts
        };

    public AppSettings ToSettings(SettingsSecrets secrets) =>
        new()
        {
            Language = Language,
            SteamId = SteamId,
            IgdbClientId = IgdbClientId,
            CoverQualityMode = CoverQualityMode,
            UiFontScale = UiFontScale,
            ThemeMode = ThemeMode,
            LibraryViewMode = LibraryViewMode,
            GridCardSize = GridCardSize,
            PageSlideEnabled = PageSlideEnabled,
            RefreshLibraryOnStartup = RefreshLibraryOnStartup,
            DismissSteamApiKeyPrompt = DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = DismissEaLibraryPrompt,
            DismissLegendaryPrompt = DismissLegendaryPrompt,
            EpicAccountId = EpicAccountId,
            EpicDisplayName = EpicDisplayName,
            XboxGamertag = XboxGamertag,
            BattleNetDisplayName = BattleNetDisplayName,
            DiscordApplicationId = DiscordApplicationId,
            DiscordConsoleNotificationsEnabled = DiscordConsoleNotificationsEnabled,
            SuppressSystemDiscordToasts = SuppressSystemDiscordToasts,
            SteamApiKey = secrets.SteamApiKey,
            IgdbClientSecret = secrets.IgdbClientSecret,
            SteamGridDbApiKey = secrets.SteamGridDbApiKey
        };
}
