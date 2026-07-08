using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Services.Updates;

namespace OpenGameHUB.ViewModels;

public partial class SettingsUpdatesViewModel : ViewModelBase
{
    private readonly Action<string> _setStatusMessage;
    private AppReleaseInfo? _pendingRelease;
    private CancellationTokenSource? _updateCts;

    public SettingsUpdatesViewModel(Action<string> setStatusMessage)
    {
        _setStatusMessage = setStatusMessage;
        RefreshLocalizedText();
        _ = CheckForUpdatesAsync();
    }

    [ObservableProperty]
    private string _appVersionText = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isUpdateBusy;

    [ObservableProperty]
    private double _updateProgress;

    public bool CanInstallUpdate => _pendingRelease is not null && !IsUpdateBusy;

    public bool ShowIndeterminateUpdateProgress => IsUpdateBusy && UpdateProgress <= 0;

    public void RefreshLocalizedText() =>
        AppVersionText = Loc.T("AppCurrentVersion", AppUpdateService.CurrentVersion);

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
                _setStatusMessage(Loc.T("AppUpdateCheckFailed"));
                return;
            }

            if (AppUpdateService.IsNewer(release.TagName, AppUpdateService.CurrentVersion))
            {
                _pendingRelease = release;
                IsUpdateAvailable = true;
                _setStatusMessage(Loc.T("AppUpdateAvailable", release.TagName));
            }
            else
            {
                _pendingRelease = null;
                IsUpdateAvailable = false;
                _setStatusMessage(Loc.T("AppUpdateUpToDate", AppUpdateService.CurrentVersion));
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
            _setStatusMessage(Loc.T("AppUpdateCheckFailedDetail", ex.Message));
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
                _setStatusMessage(Loc.T("AppUpdateDownloading", value));
            });

            _setStatusMessage(Loc.T("AppUpdateInstalling"));
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
            _setStatusMessage(Loc.T("AppUpdateDownloadFailed", ex.Message));
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
}
