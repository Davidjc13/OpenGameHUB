using System.Diagnostics;
using OpenGameHUB.Services.Gog;

namespace OpenGameHUB.Services;

internal static class GogLauncherClient
{
    public static void StartInstall(string releaseKey, string? productId)
    {
        if (!GogCatalogReader.IsLauncherInstalled())
            throw new InvalidOperationException(Loc.T("GogLauncherNotInstalled"));

        if (string.IsNullOrWhiteSpace(releaseKey))
            throw new InvalidOperationException(Loc.T("GogInstallUrlMissing"));

        if (GogCatalogReader.IsGalaxyRunning()
            && !string.IsNullOrWhiteSpace(productId)
            && TryStartInstallCommand(productId))
        {
            return;
        }

        StartProtocol(BuildOpenGameViewUrl(releaseKey));
    }

    private static bool TryStartInstallCommand(string productId)
    {
        var clientPath = GogCatalogReader.FindLauncherExecutable();
        if (clientPath is null)
            return false;

        try
        {
            StartProcess(clientPath, $"/gameId={productId} /command=installGame", Path.GetDirectoryName(clientPath));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildOpenGameViewUrl(string releaseKey) =>
        $"goggalaxy://openGameView/{releaseKey}";

    private static void StartProtocol(string url)
    {
        try
        {
            StartProcess(url, null, null, useShellExecute: true);
        }
        catch
        {
            StartProcess("cmd.exe", $"/c start \"\" \"{url}\"", null, useShellExecute: false);
        }
    }

    private static void StartProcess(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool useShellExecute = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = useShellExecute
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
    }
}
