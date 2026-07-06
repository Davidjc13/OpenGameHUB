using System.Diagnostics;

namespace OpenGameHUB.Providers.Epic;

internal static class EpicLauncherClient
{
    public static void StartInstall(string protocolUrl)
    {
        if (!LegendaryClient.IsEpicLauncherInstalled())
            throw new InvalidOperationException(Loc.T("EpicLauncherNotInstalled"));

        if (string.IsNullOrWhiteSpace(protocolUrl))
            throw new InvalidOperationException(Loc.T("EpicInstallUrlMissing"));

        var psi = new ProcessStartInfo
        {
            FileName = protocolUrl,
            UseShellExecute = true
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("EpicInstallLaunchFailed"));
    }
}
