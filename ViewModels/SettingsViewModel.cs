using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Localization;
using OpenGameHUB.Providers.Epic;
using OpenGameHUB.Providers.Xbox;
using OpenGameHUB.Services.Auth;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly Action? _onDevSessionReset;
    private readonly Action? _onDevRelaunchRequested;
    private readonly Action? _onDevClearLocalDatabase;
    private AppReleaseInfo? _pendingRelease;
    private CancellationTokenSource? _updateCts;

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
        SelectedCoverQuality = current.CoverQualityMode;
        SelectedLanguage = LocalizationService.ResolveLanguage(current.Language);
        Strings = new LocalizedStrings();
        CoverQualityOptions = BuildCoverQualityOptions();
        RefreshSteamStatus();
        RefreshEpicStatus();
        RefreshXboxStatus();
        AppVersionText = Loc.T("AppCurrentVersion", AppUpdateService.CurrentVersion);
        _ = CheckForUpdatesAsync();
    }

    public string AppVersionText { get; private set; } = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isUpdateBusy;

    [ObservableProperty]
    private double _updateProgress;

    public bool CanInstallUpdate => _pendingRelease is not null && !IsUpdateBusy;

    public bool ShowIndeterminateUpdateProgress => IsUpdateBusy && UpdateProgress <= 0;

    public bool IsSteamApiConnected => _settingsService.Current.IsSteamApiConfigured;

    public bool IsEpicSectionVisible =>
        LegendaryClient.IsEpicLauncherInstalled() || LegendaryClient.IsAvailable();

    public bool IsEpicConnected =>
        LegendaryClient.HasStoredCredentials()
        || _settingsService.Current.HasEpicAuth;

    public bool IsXboxConnected => XboxAccountClient.IsAuthenticated();

    public bool CanConnectXbox => !IsXboxConnected;

    public bool CanConnectEpic => LegendaryClient.IsAvailable() && !IsEpicConnected;

    public bool IsDevModeEnabled => DevModeService.IsEnabled;

    public LocalizedStrings Strings { get; }

    [ObservableProperty]
    private string _steamStatusText = string.Empty;

    [ObservableProperty]
    private string _epicStatusText = string.Empty;

    [ObservableProperty]
    private string _xboxStatusText = string.Empty;

    [ObservableProperty]
    private string _igdbClientId = string.Empty;

    [ObservableProperty]
    private string _igdbClientSecret = string.Empty;

    [ObservableProperty]
    private string _steamGridDbApiKey = string.Empty;

    [ObservableProperty]
    private CoverQualityMode _selectedCoverQuality = CoverQualityMode.Low;

    partial void OnSelectedCoverQualityChanged(CoverQualityMode value) =>
        OnPropertyChanged(nameof(SelectedCoverQualityOption));

    public CoverQualityOption? SelectedCoverQualityOption
    {
        get => CoverQualityOptions.FirstOrDefault(option => option.Mode == SelectedCoverQuality);
        set
        {
            if (value is null || value.Mode == SelectedCoverQuality)
                return;

            SelectedCoverQuality = value.Mode;
            OnPropertyChanged(nameof(SelectedCoverQualityOption));
        }
    }

    public IReadOnlyList<CoverQualityOption> CoverQualityOptions { get; }

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
            CoverQualityMode = current.CoverQualityMode,
            DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
            DismissLegendaryPrompt = current.DismissLegendaryPrompt,
            EpicAccountId = current.EpicAccountId,
            EpicDisplayName = current.EpicDisplayName,
            XboxGamertag = current.XboxGamertag
        });

        RefreshSteamStatus();
        OnPropertyChanged(nameof(IsSteamApiConnected));
        StatusMessage = Loc.T("SteamApiDisconnected");
    }

    [RelayCommand]
    private async Task ConnectXboxAsync()
    {
        try
        {
            StatusMessage = Loc.T("XboxAuthStarted");
            await XboxAuthService.SignInAsync(_settingsService, GetOwnerWindow());
            RefreshXboxStatus();
            OnPropertyChanged(nameof(IsXboxConnected));
            OnPropertyChanged(nameof(CanConnectXbox));
            StatusMessage = Loc.T("XboxConnected");
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.T("XboxConnectFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void DisconnectXbox()
    {
        XboxAuthHelper.Clear(_settingsService);
        RefreshXboxStatus();
        OnPropertyChanged(nameof(IsXboxConnected));
        OnPropertyChanged(nameof(CanConnectXbox));
        StatusMessage = Loc.T("XboxDisconnected");
    }

    private void RefreshXboxStatus()
    {
        if (!IsXboxConnected)
        {
            XboxStatusText = Loc.T("XboxNotConnectedStatus");
            return;
        }

        var gamertag = _settingsService.Current.XboxGamertag;
        XboxStatusText = string.IsNullOrWhiteSpace(gamertag)
            ? Loc.T("XboxConnectedStatus")
            : Loc.T("XboxConnectedStatusNamed", gamertag);
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

            if (EmbeddedBrowserService.IsAvailable)
            {
                var authCode = await EmbeddedBrowserService.ShowCaptureAsync<string>(
                    new EpicAuthCaptureStrategy(),
                    GetOwnerWindow());

                if (string.IsNullOrWhiteSpace(authCode))
                {
                    StatusMessage = Loc.T("EpicAuthCancelled");
                    return;
                }

                using var authCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                await LegendaryClient.RunAuthWithCodeAsync(authCode, authCts.Token);
                EpicAuthHelper.PersistFromLegendary(_settingsService);
                RefreshEpicStatus();
                OnPropertyChanged(nameof(IsEpicConnected));
                OnPropertyChanged(nameof(CanConnectEpic));
                StatusMessage = Loc.T("EpicAuthCompleted");
                return;
            }

            LegendaryClient.RunAuth();
            StatusMessage = Loc.T("EpicAuthStartedFallback");
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

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        _updateCts?.Cancel();
        _updateCts = new CancellationTokenSource();
        var cancellationToken = _updateCts.Token;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        InstallUpdateCommand.NotifyCanExecuteChanged();

        try
        {
            var release = await AppUpdateService.GetLatestReleaseAsync(cancellationToken);
            if (release is null)
            {
                _pendingRelease = null;
                IsUpdateAvailable = false;
                StatusMessage = Loc.T("AppUpdateCheckFailed");
                return;
            }

            if (AppUpdateService.IsNewer(release.TagName, AppUpdateService.CurrentVersion))
            {
                _pendingRelease = release;
                IsUpdateAvailable = true;
                StatusMessage = Loc.T("AppUpdateAvailable", release.TagName);
            }
            else
            {
                _pendingRelease = null;
                IsUpdateAvailable = false;
                StatusMessage = Loc.T("AppUpdateUpToDate", AppUpdateService.CurrentVersion);
            }
        }
        catch (OperationCanceledException)
        {
            // optional
        }
        catch (Exception ex)
        {
            _pendingRelease = null;
            IsUpdateAvailable = false;
            StatusMessage = Loc.T("AppUpdateCheckFailedDetail", ex.Message);
        }
        finally
        {
            IsUpdateBusy = false;
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_pendingRelease is null || IsUpdateBusy)
            return;

        _updateCts?.Cancel();
        _updateCts = new CancellationTokenSource();
        var cancellationToken = _updateCts.Token;

        IsUpdateBusy = true;
        UpdateProgress = 0;
        InstallUpdateCommand.NotifyCanExecuteChanged();

        try
        {
            var progress = new Progress<double>(value =>
            {
                UpdateProgress = value;
                StatusMessage = Loc.T("AppUpdateDownloading", value);
            });

            StatusMessage = Loc.T("AppUpdateInstalling");
            await AppUpdateService.DownloadAndInstallAsync(
                _pendingRelease,
                progress,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // optional
        }
        catch (Exception ex)
        {
            StatusMessage = Loc.T("AppUpdateDownloadFailed", ex.Message);
        }
        finally
        {
            IsUpdateBusy = false;
            InstallUpdateCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnIsUpdateBusyChanged(bool value)
    {
        InstallUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowIndeterminateUpdateProgress));
    }

    partial void OnUpdateProgressChanged(double value) =>
        OnPropertyChanged(nameof(ShowIndeterminateUpdateProgress));

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
            CoverQualityMode = SelectedCoverQuality,
            LibraryViewMode = current.LibraryViewMode,
            DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt,
            DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
            DismissLegendaryPrompt = current.DismissLegendaryPrompt,
            EpicAccountId = current.EpicAccountId,
            EpicDisplayName = current.EpicDisplayName,
            XboxGamertag = current.XboxGamertag
        });

        Loc.Service.SetLanguage(_settingsService.Current.Language);
        StatusMessage = Loc.T("SettingsSaved");
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private static IReadOnlyList<CoverQualityOption> BuildCoverQualityOptions() =>
    [
        new(CoverQualityMode.None, Loc.T("CoverQualityNone")),
        new(CoverQualityMode.Low, Loc.T("CoverQualityLow")),
        new(CoverQualityMode.High, Loc.T("CoverQualityHigh"))
    ];
}

public sealed record LanguageOption(string Code, string Label);

public sealed record CoverQualityOption(CoverQualityMode Mode, string Label);
