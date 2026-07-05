using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Models;
using OpenGameHUB.Services;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var current = settingsService.Current;
        IgdbClientId = current.IgdbClientId;
        IgdbClientSecret = current.IgdbClientSecret;
        SteamGridDbApiKey = current.SteamGridDbApiKey;
        ShowGridCovers = current.ShowGridCovers;
        SelectedLanguage = LocalizationService.ResolveLanguage(current.Language);
        Strings = new LocalizedStrings();
        RefreshSteamStatus();
    }

    public bool IsSteamApiConnected => _settingsService.Current.IsSteamApiConfigured;

    public LocalizedStrings Strings { get; }

    [ObservableProperty]
    private string _steamStatusText = string.Empty;

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
    private async Task OpenSteamSetupAsync()
    {
        var window = new SteamSetupWindow(new SteamSetupViewModel(_settingsService));
        await window.ShowDialog(GetOwnerWindow());
        RefreshSteamStatus();
        OnPropertyChanged(nameof(IsSteamApiConnected));
    }

    private static Window GetOwnerWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { } owner)
        {
            return owner;
        }

        throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));
    }

    [RelayCommand]
    private void ClearSteamApi()
    {
        var current = _settingsService.Current;
        _settingsService.Save(new AppSettings
        {
            Language = current.Language,
            SteamApiKey = string.Empty,
            SteamId = string.Empty,
            IgdbClientId = current.IgdbClientId,
            IgdbClientSecret = current.IgdbClientSecret,
            SteamGridDbApiKey = current.SteamGridDbApiKey,
            ShowGridCovers = current.ShowGridCovers,
            DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt
        });

        RefreshSteamStatus();
        OnPropertyChanged(nameof(IsSteamApiConnected));
        StatusMessage = Loc.T("SteamApiDisconnected");
    }

    private void RefreshSteamStatus()
    {
        var settings = _settingsService.Current;
        if (settings.IsSteamApiConfigured)
        {
            SteamStatusText = Loc.T("SteamConfiguredStatus", settings.SteamId);
            return;
        }

        SteamStatusText = SteamLocalAccountReader.IsSteamInstalled
            ? Loc.T("SteamLocalLibraryStatus")
            : Loc.T("SteamNotConfigured");
    }

    [RelayCommand]
    private void Save()
    {
        var current = _settingsService.Current;
        _settingsService.Save(new AppSettings
        {
            Language = SelectedLanguage,
            SteamApiKey = current.SteamApiKey,
            SteamId = current.SteamId,
            IgdbClientId = IgdbClientId.Trim(),
            IgdbClientSecret = IgdbClientSecret.Trim(),
            SteamGridDbApiKey = SteamGridDbApiKey.Trim(),
            ShowGridCovers = ShowGridCovers,
            DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt
        });

        Loc.Service.SetLanguage(_settingsService.Current.Language);
        StatusMessage = Loc.T("SettingsSaved");
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}

public sealed record LanguageOption(string Code, string Label);
