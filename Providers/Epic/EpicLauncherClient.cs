using System.Diagnostics;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Epic;

internal static class EpicLauncherClient
{
    private static readonly string[] ProcessNames = ["EpicGamesLauncher"];

    private static readonly TimeSpan ColdStartWindowTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReadySettleDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ConfirmationTimeoutPerAttempt = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ConfirmationPollInterval = TimeSpan.FromMilliseconds(500);

    // At most two sends, and the second only fires when the log did not confirm the first.
    // This keeps a single visible install prompt in the common case while still recovering
    // from a deep link that Epic dropped during cold start.
    private const int MaxProtocolAttempts = 2;

    public static bool IsEpicLauncherRunning() => TryGetLauncherProcess() is not null;

    public static async Task StartInstallAsync(
        string protocolUrl,
        CancellationToken cancellationToken = default)
    {
        if (!LegendaryClient.IsEpicLauncherInstalled())
            throw new InvalidOperationException(Loc.T("EpicLauncherNotInstalled"));

        if (string.IsNullOrWhiteSpace(protocolUrl))
            throw new InvalidOperationException(Loc.T("EpicInstallUrlMissing"));

        var alreadyRunning = IsEpicLauncherRunning();
        if (!alreadyRunning)
        {
            var launcherExe = LegendaryClient.FindEpicLauncherExecutable()
                ?? throw new InvalidOperationException(Loc.T("EpicLauncherNotInstalled"));

            // Start the launcher itself (without the deep link, which Epic ignores while
            // it is still booting) and wait until its UI is actually responsive.
            StartLauncher(launcherExe);
            await WaitForEpicLauncherReadyAsync(ColdStartWindowTimeout, cancellationToken);
            await Task.Delay(ReadySettleDelay, cancellationToken);
        }

        await SendInstallRequestAsync(protocolUrl, singleShot: alreadyRunning, cancellationToken);
    }

    private static async Task SendInstallRequestAsync(
        string protocolUrl,
        bool singleShot,
        CancellationToken cancellationToken)
    {
        var logWatcher = new EpicLogWatcher(ExtractAppName(protocolUrl));

        // If Epic was already running we know its protocol handler is live, so one send is
        // enough. If we cannot read Epic's log we cannot tell whether a resend is needed, so
        // we also send just once to avoid opening the install prompt more than once.
        if (singleShot || !logWatcher.IsAvailable)
        {
            StartProtocol(protocolUrl);
            return;
        }

        for (var attempt = 0; attempt < MaxProtocolAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartProtocol(protocolUrl);

            if (await logWatcher.WaitForInstallRequestAsync(
                    ConfirmationTimeoutPerAttempt,
                    ConfirmationPollInterval,
                    cancellationToken))
            {
                return;
            }
        }
    }

    private static async Task<bool> WaitForEpicLauncherReadyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsLauncherReady())
                return true;

            await Task.Delay(500, cancellationToken);
        }

        return IsLauncherReady();
    }

    private static bool IsLauncherReady()
    {
        var process = TryGetLauncherProcess();
        if (process is null)
            return false;

        try
        {
            process.Refresh();
            if (process.MainWindowHandle == IntPtr.Zero)
                return false;

            try
            {
                process.WaitForInputIdle(1000);
            }
            catch
            {
                // WaitForInputIdle is best-effort; a visible main window is still a good signal.
            }

            return true;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(EpicLauncherClient),
                operation: "IsLauncherReady",
                exception: ex,
                platform: Platform.Epic);
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

    private static string? ExtractAppName(string protocolUrl)
    {
        // com.epicgames.launcher://apps/{namespace}%3A{catalog}%3A{appName}?action=install
        var appsIndex = protocolUrl.IndexOf("apps/", StringComparison.OrdinalIgnoreCase);
        if (appsIndex < 0)
            return null;

        var rest = protocolUrl[(appsIndex + "apps/".Length)..];

        var queryIndex = rest.IndexOf('?');
        if (queryIndex >= 0)
            rest = rest[..queryIndex];

        rest = Uri.UnescapeDataString(rest);

        var parts = rest.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var appName = parts.Length > 0 ? parts[^1] : rest;

        return string.IsNullOrWhiteSpace(appName) ? null : appName;
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

    /// <summary>
    /// Watches Epic's launcher log for a reference to the requested app, which appears once
    /// the launcher accepts the install deep link. Used to stop resending the protocol.
    /// </summary>
    private sealed class EpicLogWatcher
    {
        private readonly string? _logPath;
        private readonly string? _token;
        private long _position;

        public EpicLogWatcher(string? token)
        {
            _token = token;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidate = Path.Combine(
                localAppData,
                "EpicGamesLauncher",
                "Saved",
                "Logs",
                "EpicGamesLauncher.log");

            if (File.Exists(candidate))
            {
                _logPath = candidate;
                try
                {
                    _position = new FileInfo(candidate).Length;
                }
                catch
                {
                    _position = 0;
                }
            }
        }

        public bool IsAvailable => _logPath is not null && !string.IsNullOrWhiteSpace(_token);

        public async Task<bool> WaitForInstallRequestAsync(
            TimeSpan timeout,
            TimeSpan pollInterval,
            CancellationToken cancellationToken)
        {
            if (!IsAvailable)
                return false;

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (HasNewMatch())
                    return true;

                await Task.Delay(pollInterval, cancellationToken);
            }

            return HasNewMatch();
        }

        private bool HasNewMatch()
        {
            try
            {
                using var stream = new FileStream(
                    _logPath!,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                if (stream.Length < _position)
                    _position = 0; // log rotated

                if (stream.Length == _position)
                    return false;

                stream.Seek(_position, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                _position = stream.Length;

                return text.Contains(_token!, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
