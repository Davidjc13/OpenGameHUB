using Microsoft.Win32;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services.LibraryProviders;

public sealed class SteamCloudLibraryProvider : ICloudLibraryProvider
{
    private readonly SettingsService _settingsService;
    private readonly SteamWebApiService _steamWebApiService;
    private IReadOnlyList<SteamWebApiService.SteamOwnedGameEntry> _ownedGames = [];

    public SteamCloudLibraryProvider(SettingsService settingsService, SteamWebApiService steamWebApiService)
    {
        _settingsService = settingsService;
        _steamWebApiService = steamWebApiService;
    }

    public Platform Platform => Platform.Steam;

    public bool IsAvailable() => _settingsService.Current.IsSteamApiConfigured;

    public void SetOwnedGames(IReadOnlyList<SteamWebApiService.SteamOwnedGameEntry> ownedGames) =>
        _ownedGames = ownedGames;

    public void ClearOwnedGames() => _ownedGames = [];

    public IReadOnlyList<UnifiedGame> GetUninstalledLibraryGames(
        IReadOnlyList<UnifiedGame> currentGames,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_ownedGames.Count == 0)
            return [];

        return _steamWebApiService.GetUninstalledOwnedGames(_ownedGames, currentGames);
    }

    public IEnumerable<Action> GetInstallLaunchAttempts(UnifiedGame game)
    {
        if (game.Platform != Platform.Steam || game.IsInstalled)
            yield break;

        if (!int.TryParse(game.PlatformGameId, out var appId))
            yield break;

        yield return () => StartProtocol($"steam://install/{appId}");

        var steamExe = FindSteamExecutable();
        if (steamExe is not null)
            yield return () => StartLauncherArgs(steamExe, $"steam://install/{appId}");
    }

    private static string? FindSteamExecutable()
    {
        const string defaultPath = @"C:\Program Files (x86)\Steam\steam.exe";
        if (File.Exists(defaultPath))
            return defaultPath;

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            using var steamKey = baseKey.OpenSubKey(@"Software\Valve\Steam");
            var steamPath = steamKey?.GetValue("SteamPath") as string;
            if (string.IsNullOrWhiteSpace(steamPath))
                continue;

            var candidate = Path.Combine(steamPath.Replace('/', Path.DirectorySeparatorChar), "steam.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

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

    private static void StartLauncherArgs(string launcherExe, string protocolUrl)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = launcherExe,
            Arguments = protocolUrl,
            WorkingDirectory = Path.GetDirectoryName(launcherExe) ?? string.Empty,
            UseShellExecute = false
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", launcherExe));
    }

    private static void StartProcess(
        string fileName,
        string? arguments,
        string? workingDirectory,
        bool useShellExecute)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = useShellExecute
        };

        if (System.Diagnostics.Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("ProcessStartFailed", fileName));
    }
}
