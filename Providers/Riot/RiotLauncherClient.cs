using System.Diagnostics;
using System.Text.Json;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Providers.Riot;

internal static class RiotLauncherClient
{
    private static readonly string[] InstallBlockerProcessNames =
    [
        "RiotClientUx",
        "RiotClientUxRender",
        "RiotClientCrashHandler"
    ];

    public static void StartInstall(UnifiedGame game)
    {
        var productId = ResolveProductId(game)
            ?? throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        var patchline = ResolvePatchline(game) ?? "live";
        StartInstall(productId, patchline);
    }

    public static void StartInstall(string productId, string patchline = "live")
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        var clientExe = RiotCatalogReader.FindClientServicesExecutable()
            ?? throw new InvalidOperationException(Loc.T("RiotClientNotInstalled"));

        EnsureClientReadyForInstall();
        var arguments = RiotCatalogReader.BuildLaunchArguments(productId, patchline, install: true);
        StartClient(clientExe, arguments);
    }

    public static void StartLaunch(string productId, string patchline = "live")
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        var clientExe = RiotCatalogReader.FindClientServicesExecutable()
            ?? throw new InvalidOperationException(Loc.T("RiotClientNotInstalled"));

        var arguments = RiotCatalogReader.BuildLaunchArguments(productId, patchline, install: false);
        StartClient(clientExe, arguments);
    }

    public static string? ResolveProductId(UnifiedGame game)
    {
        if (!string.IsNullOrWhiteSpace(game.PlatformGameId)
            && !game.PlatformGameId.Contains('.', StringComparison.Ordinal))
        {
            return game.PlatformGameId;
        }

        if (TryGetCatalogProductId(game.Id, out var catalogProductId))
            return catalogProductId;

        return RiotCatalogReader.TryGetLaunchProductFromSpec(game.LaunchSpec, out var launchProduct)
            ? launchProduct
            : null;
    }

    public static string? ResolvePatchline(UnifiedGame game)
    {
        if (TryGetCatalogPatchline(game.Id, out var patchline))
            return patchline;

        return RiotCatalogReader.TryGetLaunchPatchlineFromSpec(game.LaunchSpec, out var launchPatchline)
            ? launchPatchline
            : "live";
    }

    private static bool TryGetCatalogProductId(string id, out string productId)
    {
        productId = string.Empty;
        const string prefix = "riot:catalog:";
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        productId = separator >= 0 ? payload[..separator] : payload;
        return productId.Length > 0;
    }

    private static bool TryGetCatalogPatchline(string id, out string patchline)
    {
        patchline = string.Empty;
        const string prefix = "riot:catalog:";
        if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var payload = id[prefix.Length..];
        var separator = payload.IndexOf('@');
        if (separator < 0 || separator >= payload.Length - 1)
            return false;

        patchline = payload[(separator + 1)..];
        return patchline.Length > 0;
    }

    private static void EnsureClientReadyForInstall()
    {
        foreach (var processName in InstallBlockerProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    process.CloseMainWindow();
                    if (!process.WaitForExit(2000))
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        Thread.Sleep(750);
    }

    private static void StartClient(string clientExe, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = clientExe,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(clientExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", clientExe));
    }
}
