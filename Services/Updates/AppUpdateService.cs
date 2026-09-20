using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGameHUB.Infrastructure.Security;

namespace OpenGameHUB.Services.Updates;

public sealed record AppReleaseInfo(
    string TagName,
    string HtmlUrl,
    string DownloadUrl,
    string AssetName,
    long AssetSizeBytes,
    string? ChecksumDownloadUrl);

public static class AppUpdateService
{
    private const string Repository = "Davidjc13/OpenGameHUB";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{Repository}/releases?per_page=30";
    private const string InstallerAssetPrefix = "OpenGameHUB-Setup-";
    private const string ChecksumAssetSuffix = ".sha256";

    public static readonly TimeSpan BackgroundCheckInterval = TimeSpan.FromHours(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = CreateHttpClient();

    public static string CurrentVersion => ResolveCurrentVersion();

    public static bool IsDevBuild =>
        string.IsNullOrWhiteSpace(CurrentVersion)
        || string.Equals(CurrentVersion, "dev", StringComparison.OrdinalIgnoreCase);

    public static async Task<AppReleaseInfo?> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(ReleasesApiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken);
        if (releases is null || releases.Count == 0)
            return null;

        AppReleaseInfo? best = null;
        foreach (var release in releases)
        {
            if (release.Draft)
                continue;

            var candidate = TryMapRelease(release);
            if (candidate is null)
                continue;

            if (best is null || ReleaseVersionComparer.Compare(candidate.TagName, best.TagName) > 0)
                best = candidate;
        }

        return best;
    }

    internal static AppReleaseInfo? TryMapRelease(GitHubRelease release)
    {
        if (string.IsNullOrWhiteSpace(release.TagName))
            return null;

        var asset = release.Assets?
            .FirstOrDefault(a =>
                a.Name.StartsWith(InstallerAssetPrefix, StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));

        if (asset is null)
            return null;

        var checksumName = asset.Name + ChecksumAssetSuffix;
        var checksumAsset = release.Assets?
            .FirstOrDefault(a =>
                string.Equals(a.Name, checksumName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));

        return new AppReleaseInfo(
            release.TagName.Trim(),
            release.HtmlUrl ?? $"https://github.com/{Repository}/releases/tag/{release.TagName}",
            asset.BrowserDownloadUrl!,
            asset.Name,
            asset.Size,
            checksumAsset?.BrowserDownloadUrl);
    }

    public static bool IsNewer(string latestTag, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestTag))
            return false;

        if (IsDevBuild)
            return true;

        return ReleaseVersionComparer.Compare(latestTag, currentVersion) > 0;
    }

    public static Task<string> DownloadInstallerAsync(
        AppReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) =>
        DownloadInstallerAsync(Http, release, progress, cancellationToken);

    internal static async Task<string> DownloadInstallerAsync(
        HttpClient http,
        AppReleaseInfo release,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(release.ChecksumDownloadUrl))
            throw new InvalidOperationException(Loc.T("AppUpdateChecksumMissing"));

        var directory = Path.Combine(
            Path.GetTempPath(),
            "OpenGameHUB",
            "updates");

        Directory.CreateDirectory(directory);

        var targetPath = Path.Combine(directory, release.AssetName);
        if (File.Exists(targetPath))
        {
            try
            {
                File.Delete(targetPath);
            }
            catch
            {
                targetPath = Path.Combine(directory, $"{Guid.NewGuid():N}-{release.AssetName}");
            }
        }

        using var response = await http.GetAsync(
            release.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? release.AssetSizeBytes;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (var output = File.Create(targetPath))
        {
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;

                if (totalBytes > 0)
                    progress?.Report(Math.Clamp(downloaded / (double)totalBytes * 100d, 0d, 99d));
            }
        }

        try
        {
            var checksumContent = await DownloadChecksumSidecarAsync(
                http,
                release.ChecksumDownloadUrl,
                cancellationToken);
            VerifyInstallerChecksum(targetPath, checksumContent);
        }
        catch
        {
            TryDeleteIfExists(targetPath);
            throw;
        }

        progress?.Report(100d);
        return targetPath;
    }

    internal static void VerifyInstallerChecksum(string installerPath, string checksumContent)
    {
        try
        {
            var expected = FileIntegrityVerifier.ParseSha256Sidecar(checksumContent);
            FileIntegrityVerifier.VerifySha256(installerPath, expected);
        }
        catch (IntegrityVerificationException ex)
        {
            throw new IntegrityVerificationException(Loc.T("AppUpdateIntegrityFailed"), ex);
        }
    }

    public static async Task DownloadAndInstallAsync(
        AppReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installerPath = await DownloadInstallerAsync(release, progress, cancellationToken);
        LaunchInstallerAndExit(installerPath);
    }

    public static void LaunchInstallerAndExit(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException(Loc.T("AppUpdateInstallerMissing", installerPath), installerPath);

        var appExePath = ResolveInstalledExecutablePath();
        var helperPath = WriteUpdateHelperBatch(installerPath, appExePath);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = BuildDetachedLaunchArguments(helperPath),
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("AppUpdateLaunchFailed"));

        Environment.Exit(0);
    }

    internal static string BuildDetachedLaunchArguments(string helperPath) =>
        $"/c start \"\" /MIN \"{helperPath}\"";

    internal static string ResolveInstalledExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath)
            && File.Exists(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "OpenGameHUB",
            "OpenGameHUB.exe");
    }

    internal static string BuildUpdateHelperBatch(string installerPath, string appExePath)
    {
        var installer = Path.GetFullPath(installerPath);
        var appExe = Path.GetFullPath(appExePath);
        var appDir = Path.GetDirectoryName(appExe) ?? string.Empty;

        return $"""
            @echo off
            "{installer}" /SILENT /CLOSEAPPLICATIONS
            if exist "{appExe}" start "" /D "{appDir}" "{appExe}"
            """;
    }

    private static string WriteUpdateHelperBatch(string installerPath, string appExePath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenGameHUB", "updates");
        Directory.CreateDirectory(directory);

        var helperPath = Path.Combine(directory, $"apply-update-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(helperPath, BuildUpdateHelperBatch(installerPath, appExePath));
        return helperPath;
    }

    private static async Task<string> DownloadChecksumSidecarAsync(
        HttpClient http,
        string checksumUrl,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(checksumUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(Loc.T("AppUpdateChecksumMissing"));

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            throw new IntegrityVerificationException(Loc.T("AppUpdateChecksumMissing"));

        return content;
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
            // optional cleanup
        }
    }

    private static string ResolveCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            return plusIndex >= 0 ? informational[..plusIndex] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "dev";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenGameHUB-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    internal sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    internal sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
