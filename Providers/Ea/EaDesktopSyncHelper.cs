using System.Diagnostics;

namespace OpenGameHUB.Providers.Ea;

internal static class EaDesktopSyncHelper
{
    private static readonly string[] ProcessNames = ["EADesktop", "Origin"];

    public static void LaunchEaDesktop()
    {
        var eaDesktop = EaCatalogReader.FindEaDesktopExecutable();
        if (eaDesktop is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = eaDesktop,
                UseShellExecute = true
            });
        }
        catch
        {
            // optional
        }
    }

    public static bool IsEaDesktopRunning()
    {
        foreach (var processName in ProcessNames)
        {
            if (Process.GetProcessesByName(processName).Length > 0)
                return true;
        }

        return false;
    }

    public static async Task<bool> WaitForEaDesktopProcessAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (IsEaDesktopRunning())
            return true;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsEaDesktopRunning())
                return true;

            await Task.Delay(500, cancellationToken);
        }

        return IsEaDesktopRunning();
    }

    public static async Task WaitForLibraryUpdateAsync(
        EaLibraryCacheStatus baselineStatus,
        int baselineLogCount,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(Loc.T("WaitingEaLibrarySync"));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        var improvedPolls = 0;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(2000, cancellationToken);

            EaCatalogReader.InvalidateCache();
            var status = EaCatalogReader.GetCacheStatus();
            var logCount = EaCatalogReader.GetLogLibraryEntryCount();

            if (status == EaLibraryCacheStatus.Available
                && baselineStatus is not EaLibraryCacheStatus.Available)
            {
                return;
            }

            if (logCount > baselineLogCount)
                improvedPolls++;
            else
                improvedPolls = 0;

            if (improvedPolls >= 2)
                return;
        }
    }
}
