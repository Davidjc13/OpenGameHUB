using System.Diagnostics;

namespace OpenGameHUB.Infrastructure;

internal static class ProtocolLauncher
{
    public static void Start(string url)
    {
        try
        {
            StartProcess(url, useShellExecute: true);
        }
        catch
        {
            StartProtocolFallback(url);
        }
    }

    private static void StartProtocolFallback(string url)
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

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", "cmd.exe"));
    }

    private static void StartProcess(string fileName, bool useShellExecute)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = useShellExecute
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
    }
}
