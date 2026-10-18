using System.Diagnostics;

namespace OpenGameHUB.Infrastructure;

internal static class ProtocolLauncher
{
    public static Process? Start(string url)
    {
        ProtocolUri.EnsureLaunchable(url);

        try
        {
            return StartProcess(url, useShellExecute: true);
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(ProtocolLauncher),
                operation: "Start",
                exception: ex,
                details: url);
            return StartProtocolFallback(url);
        }
    }

    private static Process StartProtocolFallback(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("start");
        psi.ArgumentList.Add(string.Empty);
        psi.ArgumentList.Add(url);

        return Process.Start(psi)
            ?? throw new InvalidOperationException(Loc.T("ProcessStartFailed", "cmd.exe"));
    }

    private static Process? StartProcess(string fileName, bool useShellExecute)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = useShellExecute
        };

        var process = Process.Start(psi);
        if (process is null && !useShellExecute)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
        return process;
    }
}
