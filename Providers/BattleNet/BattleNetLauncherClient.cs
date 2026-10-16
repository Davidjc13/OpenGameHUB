using System.Diagnostics;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetLauncherClient
{
    public static void StartInstall(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        var launcherExe = BattleNetCatalogReader.FindLauncherExecutable()
            ?? throw new InvalidOperationException(Loc.T("BattleNetLauncherNotInstalled"));

        var gameCode = BattleNetCatalogReader.ResolveInstallGameCode(productId);
        var arguments = BattleNetCatalogReader.BuildInstallArguments(productId);
        AppDiagnostics.ReportInfo(
            nameof(BattleNetLauncherClient),
            "StartInstall",
            Platform.BattleNet,
            $"productId={productId} gameCode={gameCode} exe={launcherExe} args={arguments}");

        EnsureLauncherReady(launcherExe);
        StartLauncher(launcherExe, arguments);
    }

    public static void StartLaunch(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new InvalidOperationException(Loc.T("NoLaunchMethod"));

        var launcherExe = BattleNetCatalogReader.FindLauncherExecutable()
            ?? throw new InvalidOperationException(Loc.T("BattleNetLauncherNotInstalled"));

        var officialId = BattleNetCatalogReader.ResolveOfficialProductId(productId);
        var arguments = BattleNetCatalogReader.BuildLaunchArguments(officialId);
        AppDiagnostics.ReportInfo(
            nameof(BattleNetLauncherClient),
            "StartLaunch",
            Platform.BattleNet,
            $"productId={productId} officialId={officialId} exe={launcherExe} args={arguments}");

        EnsureLauncherReady(launcherExe);
        StartLauncher(launcherExe, arguments);
        if (ProtocolUri.TryBattleNetProduct(officialId, out var url))
            ProtocolLauncher.Start(url);
    }

    private static void EnsureLauncherReady(string launcherExe)
    {
        if (IsBattleNetRunning())
            return;

        StartLauncher(launcherExe, arguments: null);
        var until = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < until)
        {
            if (IsBattleNetRunning())
            {
                Thread.Sleep(4000);
                return;
            }

            Thread.Sleep(250);
        }
    }

    private static bool IsBattleNetRunning() =>
        Process.GetProcessesByName("Battle.net").Any(process =>
        {
            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
            finally
            {
                process.Dispose();
            }
        });

    private static void StartLauncher(string launcherExe, string? arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = launcherExe,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = Path.GetDirectoryName(launcherExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", launcherExe));
    }
}
