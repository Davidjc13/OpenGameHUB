using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Models;
using OpenGameHUB.Services;

namespace OpenGameHUB.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var current = settingsService.Current;
        SteamApiKey = current.SteamApiKey;
        SteamId = current.SteamId;
        IgdbClientId = current.IgdbClientId;
        IgdbClientSecret = current.IgdbClientSecret;
        SteamGridDbApiKey = current.SteamGridDbApiKey;
        ShowGridCovers = current.ShowGridCovers;
        SelectedLanguage = LocalizationService.ResolveLanguage(current.Language);
        Strings = new LocalizedStrings();
    }

    public LocalizedStrings Strings { get; }

    [ObservableProperty]
    private string _steamApiKey = string.Empty;

    [ObservableProperty]
    private string _steamId = string.Empty;

    [ObservableProperty]
    private string _igdbClientId = string.Empty;

    [ObservableProperty]
    private string _igdbClientSecret = string.Empty;

    [ObservableProperty]
    private string _steamGridDbApiKey = string.Empty;

    [ObservableProperty]
    private bool _showGridCovers = true;

    [ObservableProperty]
    private string _selectedLanguage = "en";

    partial void OnSelectedLanguageChanged(string value) =>
        OnPropertyChanged(nameof(SelectedLanguageOption));

    public LanguageOption? SelectedLanguageOption
    {
        get => LanguageOptions.FirstOrDefault(option => option.Code == SelectedLanguage);
        set
        {
            if (value is null || value.Code == SelectedLanguage)
                return;

            SelectedLanguage = value.Code;
            OnPropertyChanged(nameof(SelectedLanguageOption));
        }
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("en", Loc.T("LanguageEnglish")),
        new("es", Loc.T("LanguageSpanish"))
    ];

    public event Action? RequestClose;

    [RelayCommand]
    private void Save()
    {
        _settingsService.Save(new AppSettings
        {
            Language = SelectedLanguage,
            SteamApiKey = SteamApiKey.Trim(),
            SteamId = SteamId.Trim(),
            IgdbClientId = IgdbClientId.Trim(),
            IgdbClientSecret = IgdbClientSecret.Trim(),
            SteamGridDbApiKey = SteamGridDbApiKey.Trim(),
            ShowGridCovers = ShowGridCovers
        });

        Loc.Service.SetLanguage(_settingsService.Current.Language);
        StatusMessage = Loc.T("SettingsSaved");
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}

public sealed record LanguageOption(string Code, string Label);
