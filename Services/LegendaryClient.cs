using System.Diagnostics;
using System.Text.Json;
using OpenGameHUB.Models;

namespace OpenGameHUB.Services;

public static class LegendaryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static bool IsAvailable() => FindExecutable() is not null;

    public static async Task<IReadOnlyList<UnifiedGame>> GetCloudOnlyGamesAsync(
        IReadOnlyList<UnifiedGame> installedGames,
        CancellationToken cancellationToken = default)
    {
        var legendary = FindExecutable();
        if (legendary is null)
            return [];

        var output = await RunAsync(legendary, "list-games --json", cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var entries = JsonSerializer.Deserialize<List<LegendaryGame>>(output, JsonOptions) ?? [];
        var installedEpicTitles = installedGames
            .Where(g => g.Platform == Platform.Epic)
            .Select(g => g.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return entries
            .Where(e => !e.IsDlc && !string.IsNullOrWhiteSpace(e.AppTitle))
            .Where(e => !installedEpicTitles.Contains(e.AppTitle))
            .Select(e => MapGame(e, legendary))
            .ToList();
    }

    private static UnifiedGame MapGame(LegendaryGame entry, string legendaryExe) => new()
    {
        Id = $"epic:legendary:{entry.AppName}",
        Platform = Platform.Epic,
        PlatformGameId = entry.AppName,
        Title = entry.AppTitle,
        IsInstalled = false,
        LaunchSpec = LaunchSpec.LauncherArgs(legendaryExe, $"launch {entry.AppName}")
    };

    private static async Task<string> RunAsync(string executable, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException(Loc.T("CannotRunLegendary"));

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? Loc.T("LegendaryFailed", process.ExitCode)
                : error.Trim());

        return output;
    }

    private static string? FindExecutable()
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';', Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;

            var candidate = Path.Combine(folder.Trim(), "legendary.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private sealed class LegendaryGame
    {
        public string AppName { get; set; } = string.Empty;
        public string AppTitle { get; set; } = string.Empty;
        public bool IsDlc { get; set; }
    }
}
