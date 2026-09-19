using Avalonia.Controls;
using Avalonia.Threading;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Providers.Ea;
using OpenGameHUB.Providers.Epic;
using OpenGameHUB.Services.Games;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels.MainWindow;

public sealed class MainWindowOnboardingViewModel
{
    private readonly GameLibraryService _library;
    private readonly Func<Window> _getMainWindow;
    private readonly Func<Task> _refreshLibraryAsync;
    private readonly Action<string> _setStatusText;
    private readonly Action<TimeSpan> _scheduleStatusClear;
    private readonly Action _cancelScheduledStatusClear;
    private bool _steamApiPromptOffered;
    private bool _eaLibraryPromptOffered;
    private bool _legendaryPromptOffered;
    private int _modalDepth;
    private bool _replayOnboardingAfterSettings;

    public MainWindowOnboardingViewModel(
        GameLibraryService library,
        Func<Window> getMainWindow,
        Func<Task> refreshLibraryAsync,
        Action<string> setStatusText,
        Action<TimeSpan> scheduleStatusClear,
        Action cancelScheduledStatusClear)
    {
        _library = library;
        _getMainWindow = getMainWindow;
        _refreshLibraryAsync = refreshLibraryAsync;
        _setStatusText = setStatusText;
        _scheduleStatusClear = scheduleStatusClear;
        _cancelScheduledStatusClear = cancelScheduledStatusClear;
    }

    public bool ShouldDeferOnboarding => _modalDepth > 0;

    public bool ReplayOnboardingAfterSettings => _replayOnboardingAfterSettings;

    public void ClearReplayOnboarding() => _replayOnboardingAfterSettings = false;

    public void ResetSession()
    {
        _steamApiPromptOffered = false;
        _eaLibraryPromptOffered = false;
        _legendaryPromptOffered = false;
        _replayOnboardingAfterSettings = true;
    }

    public async Task ShowDialogAsync(Window window)
    {
        _modalDepth++;
        try
        {
            await window.ShowDialog(_getMainWindow());
        }
        finally
        {
            _modalDepth--;
        }
    }

    public async Task OfferPromptsIfNeededAsync()
    {
        await RunOnUiThreadAsync(async () =>
        {
            try
            {
                await OfferSteamApiKeyPromptIfNeededAsync();
                await OfferEaLibraryPromptIfNeededAsync();
                await OfferLegendaryPromptIfNeededAsync();
            }
            catch
            {
                // optional setup prompts must not close the app
            }
        });
    }

    private async Task OfferEaLibraryPromptIfNeededAsync()
    {
        if (_eaLibraryPromptOffered
            || !_library.ShouldOfferEaLibraryPrompt
            || _library.Settings.Current.DismissEaLibraryPrompt)
        {
            return;
        }

        _eaLibraryPromptOffered = true;
        await Task.Delay(350);

        var viewModel = new EaLibraryPromptViewModel(
            _library.Settings,
            _library.EaLibraryCacheStatus);
        var window = new EaLibraryPromptWindow(viewModel);
        await ShowDialogAsync(window);

        if (viewModel.Choice == EaLibraryPromptChoice.OpenEaApp)
            await LaunchEaAndRefreshLibraryAsync();
    }

    private async Task LaunchEaAndRefreshLibraryAsync()
    {
        EaCatalogReader.InvalidateCache();
        var baselineStatus = EaCatalogReader.GetCacheStatus();
        var baselineLogCount = EaCatalogReader.GetLogLibraryEntryCount();

        EaDesktopSyncHelper.LaunchEaDesktop();
        _cancelScheduledStatusClear();
        _setStatusText(Loc.T("WaitingEaAppLaunch"));

        if (await EaDesktopSyncHelper.WaitForEaDesktopProcessAsync(TimeSpan.FromSeconds(45)))
        {
            var progress = new Progress<string>(message => _setStatusText(message));
            await EaDesktopSyncHelper.WaitForLibraryUpdateAsync(
                baselineStatus,
                baselineLogCount,
                progress);
        }

        await _refreshLibraryAsync();
    }

    private async Task OfferLegendaryPromptIfNeededAsync()
    {
        if (_legendaryPromptOffered
            || !_library.ShouldOfferLegendaryPrompt)
        {
            return;
        }

        _legendaryPromptOffered = true;
        await Task.Delay(350);

        var viewModel = new LegendaryPromptViewModel(_library.Settings);
        var window = new LegendaryPromptWindow(viewModel);
        await ShowDialogAsync(window);

        if (viewModel.Choice == LegendaryPromptChoice.ConnectEpic)
        {
            try
            {
                _cancelScheduledStatusClear();
                _setStatusText(Loc.T("PreparingEpicLibrary"));
                await EpicAuthService.SignInAsync(_library.Settings, _getMainWindow());
                await _refreshLibraryAsync();
                _setStatusText(Loc.T("EpicAuthCompleted"));
                _scheduleStatusClear(TimeSpan.FromSeconds(8));
            }
            catch (Exception ex)
            {
                AppDiagnostics.ReportError(
                    area: nameof(MainWindowOnboardingViewModel),
                    operation: nameof(OfferLegendaryPromptIfNeededAsync),
                    exception: ex,
                    platform: Platform.Epic);
                _setStatusText(Loc.T("EpicConnectFailed", ex.Message));
                _scheduleStatusClear(TimeSpan.FromSeconds(8));
            }
        }
        else if (viewModel.Choice == LegendaryPromptChoice.OpenGuide)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/derrod/legendary",
                    UseShellExecute = true
                });
            }
            catch
            {
                // optional
            }
        }
    }

    private async Task OfferSteamApiKeyPromptIfNeededAsync()
    {
        if (_steamApiPromptOffered
            || _library.IsSteamApiConfigured
            || !SteamLocalAccountReader.IsSteamInstalled
            || _library.Settings.Current.DismissSteamApiKeyPrompt)
        {
            return;
        }

        _steamApiPromptOffered = true;
        await Task.Delay(350);

        var viewModel = new SteamApiKeyPromptViewModel(_library.Settings);
        var window = new SteamApiKeyPromptWindow(viewModel);
        await ShowDialogAsync(window);

        if (viewModel.Choice == SteamApiKeyPromptChoice.Configure)
            await OpenSteamSetupAsync();
    }

    private async Task OpenSteamSetupAsync()
    {
        var window = new SteamSetupWindow(new SteamSetupViewModel(_library.Settings));
        await ShowDialogAsync(window);

        if (_library.Settings.Current.IsSteamApiConfigured)
            await _refreshLibraryAsync();
    }

    private static async Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            await action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }
}
