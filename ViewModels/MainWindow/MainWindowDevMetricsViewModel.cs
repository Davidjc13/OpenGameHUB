using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.ViewModels.MainWindow;

public sealed partial class MainWindowDevMetricsViewModel : ObservableObject, IDisposable
{
    private readonly MainWindowLibraryViewModel _library;
    private readonly DispatcherTimer _timer;

    public MainWindowDevMetricsViewModel(MainWindowLibraryViewModel library)
    {
        _library = library;
        IsEnabled = DevModeService.IsEnabled;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        if (IsEnabled)
        {
            Refresh();
            _timer.Start();
        }
    }

    public bool IsEnabled { get; }

    [ObservableProperty]
    private string _metricsText = string.Empty;

    public void Refresh()
    {
        if (!IsEnabled)
            return;

        var snapshot = AppResourceMonitor.Capture();
        var allGames = _library.AllGames;
        var loadedCovers = CountLoadedCovers(allGames);

        MetricsText = string.Join(
            Environment.NewLine,
            Loc.T("DevMetricsTitle"),
            Loc.T(
                "DevMetricsWorkingSet",
                AppResourceFormatting.FormatMegabytes(snapshot.WorkingSetBytes)),
            Loc.T(
                "DevMetricsPrivateMemory",
                AppResourceFormatting.FormatMegabytes(snapshot.PrivateMemoryBytes)),
            Loc.T(
                "DevMetricsManagedHeap",
                AppResourceFormatting.FormatMegabytes(snapshot.ManagedHeapBytes)),
            Loc.T("DevMetricsGen0", snapshot.Gen0Collections),
            Loc.T(
                "DevMetricsThreads",
                snapshot.ProcessThreadCount,
                snapshot.ThreadPoolThreadCount),
            Loc.T(
                "DevMetricsGames",
                loadedCovers,
                allGames.Count),
            Loc.T("DevMetricsPageBound", _library.Games.Count),
            Loc.T(
                "DevMetricsState",
                _library.IsRefreshing ? Loc.T("DevMetricsYes") : Loc.T("DevMetricsNo"),
                _library.IsCoverLoadingSuppressed ? Loc.T("DevMetricsYes") : Loc.T("DevMetricsNo"),
                _library.CoverQualityMode));
    }

    public void Dispose() => _timer.Stop();

    internal static int CountLoadedCovers(IEnumerable<GameItemViewModel> games)
    {
        var count = 0;
        foreach (var game in games)
        {
            if (game.HasCover)
                count++;
        }

        return count;
    }
}
