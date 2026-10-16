using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Infrastructure.Discord;
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
        Console = new ConsoleModeViewModel(
            Library,
            ExitConsoleMode,
            discord: new DiscordIpcPresence(),
            discordApplicationId: () => _libraryService.Settings.Current.DiscordApplicationId);

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
    public ConsoleModeViewModel Console { get; }

    [ObservableProperty]
    private bool _isConsoleMode;

    [ObservableProperty]
    private string _statusText = string.Empty;

    partial void OnIsConsoleModeChanged(bool value)
    {
        if (value)
            Console.Activate();
        else
            Console.Deactivate();
    }

    [RelayCommand]
    private void ToggleFullScreen() => IsConsoleMode = !IsConsoleMode;

    private void ExitConsoleMode() => IsConsoleMode = false;

    public void ApplyWindowGamepad(ConsoleInput input)
    {
        switch (input)
        {
            case ConsoleInput.MoveLeft:
            case ConsoleInput.MoveRight:
            case ConsoleInput.MoveUp:
            case ConsoleInput.MoveDown:
                MoveLibrarySelection(input);
                break;
            case ConsoleInput.Activate:
                if (Library.SelectedGame is null)
                    MoveLibrarySelection(ConsoleInput.MoveRight);
                else
                    Library.LaunchGameCommand.Execute(Library.SelectedGame);
                break;
            case ConsoleInput.Favorite:
                Library.ToggleFavoriteCommand.Execute(Library.SelectedGame);
                break;
            case ConsoleInput.PreviousCategory:
                CyclePlatform(-1);
                break;
            case ConsoleInput.NextCategory:
                CyclePlatform(1);
                break;
            case ConsoleInput.Back:
                Library.ClearSelection();
                break;
            case ConsoleInput.MediaToggle:
            case ConsoleInput.MediaNext:
            case ConsoleInput.MediaPrevious:
                break;
        }
    }

    private void MoveLibrarySelection(ConsoleInput input)
    {
        var games = Library.Games;
        var current = Library.SelectedGame is null ? -1 : games.IndexOf(Library.SelectedGame);
        var columns = Library.IsListView ? 1 : Library.GridColumns;
        var step = WindowGamepadNavigation.Move(current, games.Count, columns, input);
        if (step.PageDelta != 0)
        {
            var moved = step.PageDelta < 0
                ? ChangePage(Library.CanGoPrevious, Library.PreviousPageCommand)
                : ChangePage(Library.CanGoNext, Library.NextPageCommand);
            var page = Library.Games;
            if (page.Count == 0)
                return;

            Library.SelectedGame = moved
                ? page[step.PageDelta < 0 ? page.Count - 1 : 0]
                : page[step.PageDelta < 0 ? 0 : page.Count - 1];
            return;
        }

        if (step.Index < 0 || step.Index >= games.Count)
            return;

        Library.SelectedGame = games[step.Index];
    }

    private static bool ChangePage(bool canChange, IRelayCommand command)
    {
        if (!canChange)
            return false;

        command.Execute(null);
        return true;
    }

    private void CyclePlatform(int delta)
    {
        var filters = Sidebar.PlatformFilters;
        if (filters.Count == 0)
            return;

        var index = Sidebar.SelectedPlatformFilter is null
            ? 0
            : filters.IndexOf(Sidebar.SelectedPlatformFilter);
        if (index < 0)
            index = 0;

        var next = (index + delta) % filters.Count;
        if (next < 0)
            next += filters.Count;

        Sidebar.SelectedPlatformFilter = filters[next];
    }

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
                ClearDevLocalDatabase,
                _libraryService.Collections,
                _libraryService)));
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
        Library.ReloadFavoritesFromDatabase();
        Sidebar.RebuildSortOptions();
        Sidebar.RebuildPlatformFilters();
        Sidebar.RebuildLibraryCollections();
        Updates.RefreshLocalizedText();
        Library.LocalizeGames();
        UiFontScaleService.Apply(_libraryService.Settings.Current.UiFontScale);
        ThemeModeService.Apply(_libraryService.Settings.Current.ThemeMode);
        Library.ApplyLocalization(releaseCoversOnQualityChange: true);
        Console.ApplyLocalization();
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
