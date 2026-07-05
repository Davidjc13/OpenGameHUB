using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;
using OpenGameHUB.Models;
using OpenGameHUB.Services;

namespace OpenGameHUB.ViewModels;

public partial class SteamSetupViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly SteamWebApiService _steamWebApiService = new();

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

    [RelayCommand]
    private void OpenApiKeyPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://steamcommunity.com/dev/apikey",
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c start \"\" \"https://steamcommunity.com/dev/apikey\"",
                UseShellExecute = false
            });
        }

        StatusMessage = Loc.T("SteamSetupApiKeyInstructions");
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
                ShowGridCovers = current.ShowGridCovers,
                DismissSteamApiKeyPrompt = current.DismissSteamApiKeyPrompt,
                DismissEaLibraryPrompt = current.DismissEaLibraryPrompt,
                DismissLegendaryPrompt = current.DismissLegendaryPrompt
            });

            StatusMessage = Loc.T("SteamSetupSuccess", result.GameCount);
            RequestClose?.Invoke();
        }
        finally
        {
            IsBusy = false;
            TestAndSaveCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanTestConnection() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(SteamApiKey)
        && !string.IsNullOrWhiteSpace(SteamId);

    partial void OnSteamApiKeyChanged(string value) => TestAndSaveCommand.NotifyCanExecuteChanged();

    partial void OnSteamIdChanged(string value) => TestAndSaveCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value) => TestAndSaveCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
