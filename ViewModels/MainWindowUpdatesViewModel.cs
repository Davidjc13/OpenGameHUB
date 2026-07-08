using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Services.Updates;

namespace OpenGameHUB.ViewModels;

public partial class MainWindowUpdatesViewModel : ViewModelBase
{
    private readonly Action<string> _setStatusText;
    private readonly Action<TimeSpan> _scheduleStatusClear;
    private CancellationTokenSource? _appUpdateCheckCts;
    private CancellationTokenSource? _appUpdateInstallCts;
    private AppReleaseInfo? _pendingAppUpdate;
    private string? _dismissedAppUpdateTag;

    public MainWindowUpdatesViewModel(
        Action<string> setStatusText,
        Action<TimeSpan> scheduleStatusClear)
    {
        _setStatusText = setStatusText;
        _scheduleStatusClear = scheduleStatusClear;
        RefreshLocalizedText();
        _ = CheckForAppUpdateAsync();
        StartPeriodicAppUpdateChecks();
    }

    [ObservableProperty]
    private string _appVersionText = string.Empty;

    [ObservableProperty]
    private bool _isAppUpdateBannerVisible;

    [ObservableProperty]
    private string _appUpdateBannerTag = string.Empty;

    [ObservableProperty]
    private string _appUpdateBannerText = string.Empty;

    [ObservableProperty]
    private bool _isAppUpdateInstalling;

    [ObservableProperty]
    private double _appUpdateProgress;

    public bool CanInstallAppUpdate => _pendingAppUpdate is not null && !IsAppUpdateInstalling;

    public void RefreshLocalizedText()
    {
        AppVersionText = Loc.T("AppCurrentVersion", AppUpdateService.CurrentVersion);
        UpdateAppUpdateBannerText();
    }

    private async Task CheckForAppUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (AppUpdateService.IsDevBuild)
            return;

        try
        {
            var release = await AppUpdateService.GetLatestReleaseAsync(cancellationToken);
            if (release is null || !AppUpdateService.IsNewer(release.TagName, AppUpdateService.CurrentVersion))
                return;

            _pendingAppUpdate = release;
            var showBanner = !string.Equals(_dismissedAppUpdateTag, release.TagName, StringComparison.OrdinalIgnoreCase);

            await RunOnUiThreadAsync(() =>
            {
                AppUpdateBannerTag = release.TagName;
                UpdateAppUpdateBannerText();
                IsAppUpdateBannerVisible = showBanner;
                OnPropertyChanged(nameof(CanInstallAppUpdate));
                InstallUpdateCommand.NotifyCanExecuteChanged();
                _setStatusText(Loc.T("AppUpdateAvailableHint", release.TagName));
                _scheduleStatusClear(TimeSpan.FromSeconds(12));
            });
        }
        catch (OperationCanceledException)
        {
            // optional background check
        }
        catch
        {
            // optional background check
        }
    }

    private void StartPeriodicAppUpdateChecks()
    {
        if (AppUpdateService.IsDevBuild)
            return;

        _appUpdateCheckCts?.Cancel();
        _appUpdateCheckCts = new CancellationTokenSource();
        _ = RunPeriodicAppUpdateChecksAsync(_appUpdateCheckCts.Token);
    }

    private async Task RunPeriodicAppUpdateChecksAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(AppUpdateService.BackgroundCheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await CheckForAppUpdateAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // app shutting down
        }
    }

    [RelayCommand]
    private void DismissBanner()
    {
        if (!string.IsNullOrWhiteSpace(AppUpdateBannerTag))
            _dismissedAppUpdateTag = AppUpdateBannerTag;

        IsAppUpdateBannerVisible = false;
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_pendingAppUpdate is null || IsAppUpdateInstalling)
            return;

        _appUpdateInstallCts?.Cancel();
        _appUpdateInstallCts = new CancellationTokenSource();
        var cancellationToken = _appUpdateInstallCts.Token;

        IsAppUpdateInstalling = true;
        AppUpdateProgress = 0;
        InstallUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstallAppUpdate));

        try
        {
            var progress = new Progress<double>(value =>
            {
                AppUpdateProgress = value;
                _setStatusText(Loc.T("AppUpdateDownloading", value));
            });

            _setStatusText(Loc.T("AppUpdateDownloading", 0));
            await AppUpdateService.DownloadAndInstallAsync(
                _pendingAppUpdate,
                progress,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // optional
        }
        catch (Exception ex)
        {
            _setStatusText(Loc.T("AppUpdateDownloadFailed", ex.Message));
            IsAppUpdateInstalling = false;
            AppUpdateProgress = 0;
            InstallUpdateCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanInstallAppUpdate));
        }
    }

    partial void OnIsAppUpdateInstallingChanged(bool value)
    {
        InstallUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstallAppUpdate));
    }

    private void UpdateAppUpdateBannerText() =>
        AppUpdateBannerText = string.IsNullOrWhiteSpace(AppUpdateBannerTag)
            ? string.Empty
            : Loc.T("AppUpdateBannerMessage", AppUpdateBannerTag);

    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }
}
