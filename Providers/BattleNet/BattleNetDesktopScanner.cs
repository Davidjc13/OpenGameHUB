using System.Text.RegularExpressions;
using Microsoft.Win32;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.BattleNet;

internal static class BattleNetDesktopScanner
{
    private static readonly Regex UidRegex = new(
        @"--(?:uid|game)=(?<uid>[^\s""]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<UnifiedGame> Scan()
    {
        if (!BattleNetCatalogReader.IsLauncherInstalled())
            return [];

        var launcherExe = BattleNetCatalogReader.FindLauncherExecutable();
        if (launcherExe is null)
            return [];

        var results = new List<UnifiedGame>();

        foreach (var program in ReadUninstallPrograms())
        {
            if (string.IsNullOrWhiteSpace(program.UninstallString))
                continue;

            var match = UidRegex.Match(program.UninstallString);
            if (!match.Success)
                continue;

            if (program.DisplayName.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
                || program.DisplayName.EndsWith("Beta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var uid = match.Groups["uid"].Value;
            var catalogGame = BattleNetCatalogReader.FindByUid(uid);
            if (catalogGame is null)
                continue;

            var installPath = string.IsNullOrWhiteSpace(program.InstallLocation)
                ? null
                : program.InstallLocation;
            if (installPath is not null && !Directory.Exists(installPath))
                installPath = null;

            var launchArgs = BattleNetCatalogReader.BuildLaunchArguments(catalogGame.ProductId);
            var game = new UnifiedGame
            {
                Id = $"battlenet:registry:{catalogGame.ProductId}",
                Platform = Platform.BattleNet,
                PlatformGameId = catalogGame.ProductId,
                Title = catalogGame.Name,
                IsInstalled = installPath is not null,
                InstallPath = installPath,
                LaunchSpec = LaunchSpec.LauncherArgs(launcherExe, launchArgs)
            };

            if (!GameEntryFilter.IsExcluded(game))
                results.Add(game);
        }

        AppDiagnostics.ReportInfo(
            nameof(BattleNetDesktopScanner),
            "Scan",
            Platform.BattleNet,
            $"addedCount={results.Count} titles={string.Join(",", results.Select(g => g.Title))}");

        return results;
    }

    private static IEnumerable<UninstallProgramEntry> ReadUninstallPrograms()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstallKey is null)
                continue;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var subKey = uninstallKey.OpenSubKey(subKeyName);
                if (subKey is null)
                    continue;

                var uninstallString = subKey.GetValue("UninstallString") as string;
                if (string.IsNullOrWhiteSpace(uninstallString)
                    || !uninstallString.Contains("Battle.net", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return new UninstallProgramEntry(
                    subKey.GetValue("DisplayName") as string ?? string.Empty,
                    subKey.GetValue("InstallLocation") as string ?? string.Empty,
                    uninstallString);
            }
        }
    }

    private sealed record UninstallProgramEntry(string DisplayName, string InstallLocation, string UninstallString);
}
