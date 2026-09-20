using System.Net.Http;
using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Providers.Epic;

internal static class LegendaryBootstrap
{
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

    public static Task<bool> EnsureInstalledAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        EnsureInstalledAsync(HttpClient, progress, cancellationToken);

    internal static async Task<bool> EnsureInstalledAsync(
        HttpClient http,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (IsManagedOrBundledAvailable())
            return true;

        progress?.Report(Loc.T("DownloadingLegendary"));

        try
        {
            Directory.CreateDirectory(ToolsDirectory);
            var tempPath = ManagedExecutablePath + ".download";
            TryDeleteIfExists(tempPath);

            var manifest = LegendaryManifest.Current;
            using var response = await http.GetAsync(
                manifest.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var local = File.Create(tempPath))
            {
                await remote.CopyToAsync(local, cancellationToken);
            }

            PromoteVerifiedDownload(tempPath, ManagedExecutablePath, manifest);
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

    internal static void PromoteVerifiedDownload(
        string tempPath,
        string destinationPath,
        LegendaryManifest manifest)
    {
        manifest.VerifyFile(tempPath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        File.Move(tempPath, destinationPath);
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
