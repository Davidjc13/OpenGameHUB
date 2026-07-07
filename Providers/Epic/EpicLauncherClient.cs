using System.Diagnostics;

namespace OpenGameHUB.Providers.Epic;

internal static class EpicLauncherClient
{
    private static readonly string[] ProcessNames = ["EpicGamesLauncher"];

    private static readonly TimeSpan ColdStartProcessTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ColdStartInitialDelay = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ColdStartRetryDelay = TimeSpan.FromSeconds(4);
    private const int ColdStartProtocolAttempts = 4;

    public static bool IsEpicLauncherRunning() => TryGetLauncherProcess() is not null;

    public static async Task StartInstallAsync(
        string protocolUrl,
        CancellationToken cancellationToken = default)
    {
        if (!LegendaryClient.IsEpicLauncherInstalled())
            throw new InvalidOperationException(Loc.T("EpicLauncherNotInstalled"));

        if (string.IsNullOrWhiteSpace(protocolUrl))
            throw new InvalidOperationException(Loc.T("EpicInstallUrlMissing"));

        if (IsEpicLauncherRunning())
        {
            StartProtocol(protocolUrl);
            return;
        }

        await StartInstallColdAsync(protocolUrl, cancellationToken);
    }

    private static async Task StartInstallColdAsync(
        string protocolUrl,
        CancellationToken cancellationToken)
    {
        var launcherExe = LegendaryClient.FindEpicLauncherExecutable()
            ?? throw new InvalidOperationException(Loc.T("EpicLauncherNotInstalled"));

        // Start the launcher itself (without the deep link, which Epic ignores while
        // it is still booting) and wait until its UI is actually up.
        StartLauncher(launcherExe);
        await WaitForEpicLauncherWindowAsync(ColdStartProcessTimeout, cancellationToken);

        // Give Epic's protocol handler time to register, then send the install deep link
        // a few times: a single early send is frequently dropped during cold start.
        await Task.Delay(ColdStartInitialDelay, cancellationToken);

        for (var attempt = 0; attempt < ColdStartProtocolAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartProtocol(protocolUrl);

            if (attempt < ColdStartProtocolAttempts - 1)
                await Task.Delay(ColdStartRetryDelay, cancellationToken);
        }
    }

    public static async Task<bool> WaitForEpicLauncherProcessAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (IsEpicLauncherRunning())
            return true;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsEpicLauncherRunning())
                return true;

            await Task.Delay(500, cancellationToken);
        }

        return IsEpicLauncherRunning();
    }

    private static async Task<bool> WaitForEpicLauncherWindowAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HasLauncherWindow())
                return true;

            await Task.Delay(500, cancellationToken);
        }

        return HasLauncherWindow();
    }

    private static bool HasLauncherWindow()
    {
        var process = TryGetLauncherProcess();
        if (process is null)
            return false;

        try
        {
            process.Refresh();
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static Process? TryGetLauncherProcess()
    {
        foreach (var processName in ProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length == 0)
                    continue;

                // Return the first one, dispose the rest.
                for (var i = 1; i < processes.Length; i++)
                    processes[i].Dispose();

                return processes[0];
            }
            catch
            {
                // optional
            }
        }

        return null;
    }

    private static void StartLauncher(string launcherExe)
    {
        var psi = new ProcessStartInfo
        {
            FileName = launcherExe,
            WorkingDirectory = Path.GetDirectoryName(launcherExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", launcherExe));
    }

    private static void StartProtocol(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        // ShellExecute hands protocol URIs to the launcher and often returns null on success.
        Process.Start(psi);
    }
}
