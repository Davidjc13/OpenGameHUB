using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Models;
using OpenGameHUB.Services;
using OpenGameHUB.Services.Epic;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onDevSessionReset;
    private readonly Action? _onDevRelaunchRequested;
    private readonly Action? _onDevClearLocalDatabase;

    public SettingsViewModel(
        SettingsService settingsService,
        Action? onDevSessionReset = null,
        Action? onDevRelaunchRequested = null,
        Action? onDevClearLocalDatabase = null)
    {
        _settingsService = settingsService;
        _onDevSessionReset = onDevSessionReset;
        _onDevRelaunchRequested = onDevRelaunchRequested;
        _onDevClearLocalDatabase = onDevClearLocalDatabase;
        var current = settingsService.Current;
        IgdbClientId = current.IgdbClientId;
        IgdbClientSecret = current.IgdbClientSecret;
        SteamGridDbApiKey = current.SteamGridDbApiKey;
        ShowGridCovers = current.ShowGridCovers;
        SelectedLanguage = LocalizationService.ResolveLanguage(current.Language);
        Strings = new LocalizedStrings();
        RefreshSteamStatus();
        RefreshEpicStatus();
    }

    public bool IsSteamApiConnected => _settingsService.Current.IsSteamApiConfigured;

    public bool IsEpicSectionVisible =>
        LegendaryClient.IsEpicLauncherInstalled() || LegendaryClient.IsAvailable();

    public bool IsEpicConnected =>
        LegendaryClient.HasStoredCredentials()
        || _settingsService.Current.HasEpicAuth;

    public bool CanConnectEpic => LegendaryClient.IsAvailable() && !IsEpicConnected;

    public bool IsDevModeEnabled => DevModeService.IsEnabled;

    public LocalizedStrings Strings { get; }

    [ObservableProperty]
    private string _steamStatusText = string.Empty;

    [ObservableProperty]
    private string _epicStatusText = string.Empty;

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
            DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
            DismissLegendaryPrompt = current.DismissLegendaryPrompt,
            EpicAccountId = current.EpicAccountId,
            EpicDisplayName = current.EpicDisplayName
        });

        RefreshSteamStatus();
        OnPropertyChanged(nameof(IsSteamApiConnected));
        StatusMessage = Loc.T("SteamApiDisconnected");
    }

    [RelayCommand]
    private async Task ConnectEpicAsync()
    {
        if (!LegendaryClient.IsEpicLauncherInstalled() && !LegendaryClient.IsAvailable())
        {
            StatusMessage = Loc.T("EpicLauncherNotInstalled");
            return;
        }

        try
        {
            StatusMessage = Loc.T("PreparingEpicLibrary");
            using var downloadCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await LegendaryBootstrap.EnsureInstalledAsync(null, downloadCts.Token);
            LegendaryClient.InvalidateExecutableCache();

            if (!LegendaryClient.IsAvailable())
            {
                StatusMessage = Loc.T("EpicHelperUnavailable");
                return;
            }

            LegendaryClient.RunAuth();
            StatusMessage = Loc.T("EpicAuthStarted");
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.T("EpicConnectFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DisconnectEpicAsync()
    {
        try
        {
            StatusMessage = Loc.T("EpicDisconnecting");
            LegendaryClient.ClearStoredCredentials();
            EpicAuthHelper.Clear(_settingsService);
            RefreshEpicStatus();
            OnPropertyChanged(nameof(IsEpicConnected));
            OnPropertyChanged(nameof(CanConnectEpic));

            if (LegendaryClient.IsAvailable())
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await LegendaryClient.RunDisconnectAsync(cts.Token);
            }

            StatusMessage = Loc.T("EpicDisconnected");
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.T("EpicDisconnectFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void DevResetConnections()
    {
        DevModeService.ResetPlatformConnections();
        _settingsService.Save(DevModeService.ResetConnectionSettings(_settingsService.Current));

        RefreshSteamStatus();
        RefreshEpicStatus();
        OnPropertyChanged(nameof(IsSteamApiConnected));
        OnPropertyChanged(nameof(IsEpicConnected));
        OnPropertyChanged(nameof(CanConnectEpic));

        _onDevSessionReset?.Invoke();
        StatusMessage = Loc.T("DevResetConnectionsDone");
    }

    [RelayCommand]
    private void DevResetAndRelaunch()
    {
        DevModeService.ResetPlatformConnections();
        _settingsService.Save(DevModeService.ResetConnectionSettings(_settingsService.Current));
        _onDevRelaunchRequested?.Invoke();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void DevClearLocalDatabase()
    {
        _onDevClearLocalDatabase?.Invoke();
        StatusMessage = Loc.T("DevClearLocalDatabaseDone");
    }

    private void RefreshEpicStatus()
    {
        try
        {
            if (!IsEpicSectionVisible)
            {
                EpicStatusText = Loc.T("EpicLauncherNotInstalled");
                return;
            }

            if (!LegendaryClient.IsAvailable())
            {
                EpicStatusText = Loc.T("EpicNotConnectedStatus");
                return;
            }

            if (LegendaryClient.HasStoredCredentials() || _settingsService.Current.HasEpicAuth)
            {
                var displayName = LegendaryClient.GetDisplayName()
                    ?? _settingsService.Current.EpicDisplayName;
                EpicStatusText = string.IsNullOrWhiteSpace(displayName)
                    ? Loc.T("EpicConnectedStatus")
                    : Loc.T("EpicConnectedStatusNamed", displayName);
                return;
            }

            EpicStatusText = Loc.T("EpicNotConnectedStatus");
        }
        catch (Exception ex)
        {
            EpicStatusText = Loc.T("EpicNotConnectedStatus");
            StatusMessage = Loc.T("ScanError", ex.Message);
        }
        finally
        {
            OnPropertyChanged(nameof(IsEpicConnected));
            OnPropertyChanged(nameof(CanConnectEpic));
        }
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
            DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
            DismissLegendaryPrompt = current.DismissLegendaryPrompt,
            EpicAccountId = current.EpicAccountId,
            EpicDisplayName = current.EpicDisplayName
        });

        Loc.Service.SetLanguage(_settingsService.Current.Language);
        StatusMessage = Loc.T("SettingsSaved");
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}

public sealed record LanguageOption(string Code, string Label);
