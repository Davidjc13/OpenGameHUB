using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Localization;
using OpenGameHUB.Services.Configuration;
using OpenGameHUB.Services.Games;
using OpenGameHUB.ViewModels.MainWindow;
using OpenGameHUB.Views;

namespace OpenGameHUB.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GameLibraryService _libraryService = new();
    private readonly GameInstallOrchestrator _installOrchestrator = new();
    private CancellationTokenSource? _statusClearCts;
    private bool _pendingDevRelaunch;
    private bool _pendingDevClearDatabase;

    public MainWindowViewModel()
    {
        Loc.Service.Initialize(_libraryService.Settings.Current.Language);
        Loc.Service.LanguageChanged += OnLanguageChanged;
        UiFontScaleService.Apply(_libraryService.Settings.Current.UiFontScale);
        ThemeModeService.Apply(_libraryService.Settings.Current.ThemeMode);

        Strings = new LocalizedStrings();

        MainWindowLibraryViewModel? libraryRef = null;

        Onboarding = new MainWindowOnboardingViewModel(
            _libraryService,
            GetMainWindow,
            () => libraryRef!.RefreshLibraryCommand.ExecuteAsync(null),
            text => StatusText = text,
            ScheduleStatusClear,
            CancelScheduledStatusClear);

        Sidebar = new MainWindowSidebarViewModel(
            _libraryService,
            GetMainWindow,
            window => Onboarding.ShowDialogAsync(window),
            () => libraryRef?.ApplyFilter(),
            text => StatusText = text,
            ScheduleStatusClear,
            () => libraryRef?.SelectedGame,
            game => { if (libraryRef is not null) libraryRef.SelectedGame = game; },
            games => libraryRef?.MergeGames(games));

        Library = new MainWindowLibraryViewModel(
            _libraryService,
            _installOrchestrator,
            Sidebar,
            text => StatusText = text,
            ScheduleStatusClear,
            CancelScheduledStatusClear,
            GetMainWindow,
            window => Onboarding.ShowDialogAsync(window));

        libraryRef = Library;

        Updates = new MainWindowUpdatesViewModel(
            text => StatusText = text,
            ScheduleStatusClear);

        StatusText = Loc.T("LoadingLibrary");
        Library.LoadCachedGames();
        _ = RefreshLibraryCommand.ExecuteAsync(null);
    }

    public LocalizedStrings Strings { get; }
    public MainWindowUpdatesViewModel Updates { get; }
    public MainWindowOnboardingViewModel Onboarding { get; }
    public MainWindowSidebarViewModel Sidebar { get; }
    public MainWindowLibraryViewModel Library { get; }

    [ObservableProperty]
    private string _statusText = string.Empty;

    [RelayCommand]
    private async Task RefreshLibraryAsync()
    {
        await Library.RefreshLibraryCommand.ExecuteAsync(null);
        if (!Onboarding.ShouldDeferOnboarding)
            await Onboarding.OfferPromptsIfNeededAsync();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        try
        {
            var wasEpicConnected = _libraryService.IsEpicConnected;
            await Onboarding.ShowDialogAsync(new SettingsWindow(new SettingsViewModel(
                _libraryService.Settings,
                Onboarding.ResetSession,
                ScheduleDevRelaunch,
                ClearDevLocalDatabase)));
            ApplyLocalization();

            if (_pendingDevRelaunch)
            {
                _pendingDevRelaunch = false;
                _libraryService.Dispose();
                DevModeService.ClearLocalLibraryCache();
                DevModeService.RelaunchApp();
                return;
            }

            if (_pendingDevClearDatabase)
            {
                _pendingDevClearDatabase = false;
                await RefreshLibraryCommand.ExecuteAsync(null);
            }
            else if (Onboarding.ReplayOnboardingAfterSettings)
            {
                Onboarding.ClearReplayOnboarding();
                await RefreshLibraryCommand.ExecuteAsync(null);
            }
            else if (wasEpicConnected != _libraryService.IsEpicConnected)
            {
                await RefreshLibraryCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(MainWindowViewModel),
                operation: "OpenSettingsAsync",
                exception: ex,
                details: "Settings dialog flow");
            StatusText = Loc.T("ScanError", ex.Message);
            ScheduleStatusClear(TimeSpan.FromSeconds(8));
        }
    }

    private void ScheduleDevRelaunch() => _pendingDevRelaunch = true;

    private void ClearDevLocalDatabase()
    {
        _libraryService.ResetLocalCache();
        _pendingDevClearDatabase = true;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplyLocalization);

    private void ApplyLocalization()
    {
        Strings.Refresh();
        Sidebar.RebuildSortOptions();
        Sidebar.RebuildPlatformFilters();
        Sidebar.RebuildLibraryCollections();
        Updates.RefreshLocalizedText();
        Library.LocalizeGames();
        UiFontScaleService.Apply(_libraryService.Settings.Current.UiFontScale);
        ThemeModeService.Apply(_libraryService.Settings.Current.ThemeMode);
        Library.ApplyLocalization(releaseCoversOnQualityChange: true);
    }

    private void ScheduleStatusClear(TimeSpan delay)
    {
        CancelScheduledStatusClear();
        _statusClearCts = new CancellationTokenSource();
        var token = _statusClearCts.Token;
        _ = ClearStatusAfterDelayAsync(delay, token);
    }

    private void CancelScheduledStatusClear()
    {
        _statusClearCts?.Cancel();
        _statusClearCts?.Dispose();
        _statusClearCts = null;
    }

    private async Task ClearStatusAfterDelayAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
            await Dispatcher.UIThread.InvokeAsync(() => StatusText = string.Empty);
        }
        catch (TaskCanceledException)
        {
            // A new status message was scheduled before clearing.
        }
    }

    private static Window GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow ?? throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));

        throw new InvalidOperationException(Loc.T("MainWindowUnavailable"));
    }
}
