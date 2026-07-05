using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenGameHUB.Services;

namespace OpenGameHUB.Localization;

public sealed class LocalizedStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string AppSubtitle => Loc.T("AppSubtitle");
    public string Settings => Loc.T("Settings");
    public string RefreshLibrary => Loc.T("RefreshLibrary");
    public string SearchPlaceholder => Loc.T("SearchPlaceholder");
    public string Platforms => Loc.T("Platforms");
    public string Sort => Loc.T("Sort");
    public string Filters => Loc.T("Filters");
    public string FavoritesOnly => Loc.T("FavoritesOnly");
    public string InstalledOnly => Loc.T("InstalledOnly");
    public string Detail => Loc.T("Detail");
    public string SelectGame => Loc.T("SelectGame");
    public string Play => Loc.T("Play");
    public string ToggleFavorite => Loc.T("ToggleFavorite");
    public string PreviousPage => Loc.T("PreviousPage");
    public string NextPage => Loc.T("NextPage");
    public string SettingsTitle => Loc.T("SettingsTitle");
    public string SettingsWindowTitle => Loc.T("SettingsWindowTitle");
    public string SettingsDescription => Loc.T("SettingsDescription");
    public string Language => Loc.T("Language");
    public string LanguageEnglish => Loc.T("LanguageEnglish");
    public string LanguageSpanish => Loc.T("LanguageSpanish");
    public string SteamWebApi => Loc.T("SteamWebApi");
    public string SteamApiHelp => Loc.T("SteamApiHelp");
    public string SteamSetupButton => Loc.T("SteamSetupButton");
    public string SteamSetupTitle => Loc.T("SteamSetupTitle");
    public string SteamSetupDescription => Loc.T("SteamSetupDescription");
    public string SteamSetupStepAccount => Loc.T("SteamSetupStepAccount");
    public string SteamSetupStepAccountHelp => Loc.T("SteamSetupStepAccountHelp");
    public string SteamSetupDetectAccount => Loc.T("SteamSetupDetectAccount");
    public string SteamSetupStepApiKey => Loc.T("SteamSetupStepApiKey");
    public string SteamSetupStepApiKeyHelp => Loc.T("SteamSetupStepApiKeyHelp");
    public string SteamSetupOpenApiKeyPage => Loc.T("SteamSetupOpenApiKeyPage");
    public string SteamSetupTestAndSave => Loc.T("SteamSetupTestAndSave");
    public string CoverArtSection => Loc.T("CoverArtSection");
    public string DisplaySection => Loc.T("DisplaySection");
    public string ShowGridCovers => Loc.T("ShowGridCovers");
    public string ShowGridCoversHelp => Loc.T("ShowGridCoversHelp");
    public string CoverArtHelp => Loc.T("CoverArtHelp");
    public string SteamApiKeyPlaceholder => Loc.T("SteamApiKeyPlaceholder");
    public string SteamIdPlaceholder => Loc.T("SteamIdPlaceholder");
    public string IgdbClientIdOptional => Loc.T("IgdbClientIdOptional");
    public string IgdbClientSecretOptional => Loc.T("IgdbClientSecretOptional");
    public string SteamGridDbApiKeyOptional => Loc.T("SteamGridDbApiKeyOptional");
    public string Cancel => Loc.T("Cancel");
    public string Save => Loc.T("Save");

    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
