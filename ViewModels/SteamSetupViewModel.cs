using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Localization;
using OpenGameHUB.Services.Auth;

namespace OpenGameHUB.ViewModels;

public partial class SteamSetupViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly SteamWebApiService _steamWebApiService = new();
    private Window? _ownerWindow;

    public SteamSetupViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        SteamApiKey = settingsService.Current.SteamApiKey;
        SteamId = settingsService.Current.SteamId;
        Strings = new LocalizedStrings();
        _ = DetectSteamAccountCommand.ExecuteAsync(null);
    }

    public LocalizedStrings Strings { get; }

    public bool IsSteamInstalled => SteamLocalAccountReader.IsSteamInstalled;

    public bool CanSignInWithBrowser => EmbeddedBrowserService.IsAvailable;

    [ObservableProperty]
    private string _steamApiKey = string.Empty;

    [ObservableProperty]
    private string _steamId = string.Empty;

    [ObservableProperty]
    private string _detectedAccountLabel = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasDetectedAccount;

    public event Action? RequestClose;

    public void SetOwnerWindow(Window ownerWindow) => _ownerWindow = ownerWindow;

    [RelayCommand]
    private Task DetectSteamAccount()
    {
        var account = SteamLocalAccountReader.DetectActiveAccount();
        if (account is null)
        {
            HasDetectedAccount = false;
            DetectedAccountLabel = string.Empty;
            StatusMessage = IsSteamInstalled
                ? Loc.T("SteamSetupAccountNotFound")
                : Loc.T("SteamSetupSteamNotInstalled");
            return Task.CompletedTask;
        }

        SteamId = account.SteamId64;
        HasDetectedAccount = true;

        var displayName = account.PersonaName ?? account.AccountName ?? account.SteamId64;
        DetectedAccountLabel = Loc.T("SteamSetupDetectedAccount", displayName, account.SteamId64);
        StatusMessage = string.IsNullOrWhiteSpace(SteamApiKey)
            ? Loc.T("SteamSetupApiKeyReady")
            : Loc.T("SteamSetupAccountDetected");

        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanRunSignInWithBrowser))]
    private async Task SignInWithBrowserAsync()
    {
        if (_ownerWindow is null)
        {
            StatusMessage = Loc.T("EmbeddedBrowserInitFailed", "owner window");
            return;
        }

        IsBusy = true;
        StatusMessage = Loc.T("EmbeddedBrowserLoading");

        try
        {
            EmbeddedBrowserService.EnsureAvailable();

            var captured = await EmbeddedBrowserService.ShowCaptureAsync<SteamBrowserCaptureResult>(
                new SteamAuthCaptureStrategy(),
                _ownerWindow);

            if (captured is null)
            {
                StatusMessage = Loc.T("SteamSetupBrowserCancelled");
                return;
            }

            if (!string.IsNullOrWhiteSpace(captured.SteamId))
            {
                SteamId = captured.SteamId;
                HasDetectedAccount = true;
                DetectedAccountLabel = Loc.T("SteamSetupDetectedAccount", captured.SteamId, captured.SteamId);
            }

            if (!string.IsNullOrWhiteSpace(captured.ApiKey))
                SteamApiKey = captured.ApiKey;

            if (captured.IsComplete)
            {
                StatusMessage = Loc.T("SteamSetupBrowserCaptured");
                await TestAndSaveAsync();
                return;
            }

            StatusMessage = Loc.T("EmbeddedBrowserSteamWaitingForKey");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            TestAndSaveCommand.NotifyCanExecuteChanged();
            SignInWithBrowserCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestAndSaveAsync()
    {
        IsBusy = true;
        StatusMessage = Loc.T("SteamSetupTesting");

        try
        {
            var result = await _steamWebApiService.TestConnectionAsync(SteamApiKey, SteamId);
            if (!result.Success)
            {
                StatusMessage = result.ErrorMessage ?? Loc.T("SteamSetupConnectionFailed");
                return;
            }

            var current = _settingsService.Current;
            _settingsService.Save(new AppSettings
            {
                Language = current.Language,
                SteamApiKey = SteamApiKey.Trim(),
                SteamId = SteamId.Trim(),
                IgdbClientId = current.IgdbClientId,
                IgdbClientSecret = current.IgdbClientSecret,
                SteamGridDbApiKey = current.SteamGridDbApiKey,
                CoverQualityMode = current.CoverQualityMode,
                UiFontScale = current.UiFontScale,
                DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt,
                DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
                DismissLegendaryPrompt = current.DismissLegendaryPrompt,
                EpicAccountId = current.EpicAccountId,
                EpicDisplayName = current.EpicDisplayName
            });

            StatusMessage = Loc.T("SteamSetupSuccess", result.GameCount);
            RequestClose?.Invoke();
        }
        finally
        {
            IsBusy = false;
            TestAndSaveCommand.NotifyCanExecuteChanged();
            SignInWithBrowserCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunSignInWithBrowser() => !IsBusy && CanSignInWithBrowser;

    private bool CanTestConnection() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(SteamApiKey)
        && !string.IsNullOrWhiteSpace(SteamId);

    partial void OnSteamApiKeyChanged(string value) => TestAndSaveCommand.NotifyCanExecuteChanged();

    partial void OnSteamIdChanged(string value) => TestAndSaveCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
    {
        TestAndSaveCommand.NotifyCanExecuteChanged();
        SignInWithBrowserCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
