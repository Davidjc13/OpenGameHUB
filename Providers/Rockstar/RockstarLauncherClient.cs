using System.Diagnostics;

namespace OpenGameHUB.Providers.Rockstar;

internal static class RockstarLauncherClient
{
    private static readonly string[] InstallBlockerProcessNames =
    [
        "Launcher",
        "LauncherPatcher"
    ];

    public static void StartInstall(string titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId))
            throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        var launcherExe = RockstarCatalogReader.FindLauncherExecutable()
            ?? throw new InvalidOperationException(Loc.T("RockstarLauncherNotInstalled"));

        EnsureLauncherClosedForInstall();
        var installArgs = RockstarCatalogReader.BuildInstallArguments(titleId);

        var psi = new ProcessStartInfo
        {
            FileName = launcherExe,
            Arguments = installArgs,
            WorkingDirectory = Path.GetDirectoryName(launcherExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", launcherExe));
    }

    private static void EnsureLauncherClosedForInstall()
    {
        foreach (var processName in InstallBlockerProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch
                {
                    // best effort; launcher may still accept install args
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        Thread.Sleep(750);
    }
}
