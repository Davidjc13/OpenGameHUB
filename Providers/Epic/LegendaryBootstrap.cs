using System.Net.Http;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Epic;

internal static class LegendaryBootstrap
{
    private const string DownloadUrl = "https://github.com/derrod/legendary/releases/latest/download/legendary.exe";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public static string ToolsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenGameHUB",
        "tools");

    public static string ManagedExecutablePath => Path.Combine(ToolsDirectory, "legendary.exe");

    public static string? BundledExecutablePath
    {
        get
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "legendary.exe");
            return File.Exists(bundled) ? bundled : null;
        }
    }

    public static bool IsManagedOrBundledAvailable() =>
        File.Exists(ManagedExecutablePath) || BundledExecutablePath is not null;

    public static async Task<bool> EnsureInstalledAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsManagedOrBundledAvailable())
            return true;

        progress?.Report(Loc.T("DownloadingLegendary"));

        try
        {
            Directory.CreateDirectory(ToolsDirectory);

            using var response = await HttpClient.GetAsync(
                DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken);
            var tempPath = ManagedExecutablePath + ".download";
            await using (var local = File.Create(tempPath))
            {
                await remote.CopyToAsync(local, cancellationToken);
            }

            if (File.Exists(ManagedExecutablePath))
                File.Delete(ManagedExecutablePath);

            File.Move(tempPath, ManagedExecutablePath);
            LegendaryClient.InvalidateExecutableCache();
            return true;
        }
        catch (Exception ex)
        {
            AppDiagnostics.ReportError(
                area: nameof(LegendaryBootstrap),
                operation: "EnsureInstalledAsync",
                exception: ex,
                platform: Platform.Epic,
                details: ManagedExecutablePath);
            TryDeleteIfExists(ManagedExecutablePath + ".download");
            return false;
        }
    }

    private static void TryDeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // optional
        }
    }
}
