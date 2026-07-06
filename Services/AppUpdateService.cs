using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGameHUB.Services;

public sealed record AppReleaseInfo(
    string TagName,
    string HtmlUrl,
    string DownloadUrl,
    string AssetName,
    long AssetSizeBytes);

public static class AppUpdateService
{
    private const string Repository = "Davidjc13/OpenGameHUB";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{Repository}/releases?per_page=1";
    private const string InstallerAssetPrefix = "OpenGameHUB-Setup-";

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
        var release = releases?.FirstOrDefault(r => !r.Draft);
        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        var asset = release.Assets?
            .FirstOrDefault(a =>
                a.Name.StartsWith(InstallerAssetPrefix, StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));

        if (asset is null)
            return null;

        return new AppReleaseInfo(
            release.TagName.Trim(),
            release.HtmlUrl ?? $"https://github.com/{Repository}/releases/tag/{release.TagName}",
            asset.BrowserDownloadUrl!,
            asset.Name,
            asset.Size);
    }

    public static bool IsNewer(string latestTag, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestTag))
            return false;

        if (IsDevBuild)
            return true;

        return ReleaseVersionComparer.Compare(latestTag, currentVersion) > 0;
    }

    public static async Task<string> DownloadInstallerAsync(
        AppReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
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

        using var response = await Http.GetAsync(
            release.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? release.AssetSizeBytes;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(targetPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloaded += read;

            if (totalBytes > 0)
                progress?.Report(Math.Clamp(downloaded / (double)totalBytes * 100d, 0d, 100d));
        }

        progress?.Report(100d);
        return targetPath;
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

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /CLOSEAPPLICATIONS",
            UseShellExecute = true
        };

        if (Process.Start(psi) is null)
            throw new InvalidOperationException(Loc.T("AppUpdateLaunchFailed"));

        Environment.Exit(0);
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

    private sealed class GitHubRelease
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

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}

internal static class ReleaseVersionComparer
{
    public static int Compare(string? latest, string? current)
    {
        if (TryParse(latest, out var latestParsed) && TryParse(current, out var currentParsed))
        {
            var channelCompare = ChannelRank(latestParsed.Channel).CompareTo(ChannelRank(currentParsed.Channel));
            if (channelCompare != 0)
                return channelCompare;

            return latestParsed.Version.CompareTo(currentParsed.Version);
        }

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    private static int ChannelRank(string channel) => channel switch
    {
        "stable" => 3,
        "beta" => 2,
        "alpha" => 1,
        _ => 0
    };

    private static bool TryParse(string? input, out (string Channel, Version Version) result)
    {
        result = ("", new Version(0, 0));
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var tag = input.Trim();
        string channel;
        if (tag.StartsWith("alpha-", StringComparison.OrdinalIgnoreCase))
        {
            channel = "alpha";
            tag = tag[6..];
        }
        else if (tag.StartsWith("beta-", StringComparison.OrdinalIgnoreCase))
        {
            channel = "beta";
            tag = tag[5..];
        }
        else
        {
            channel = "stable";
        }

        if (!Version.TryParse(tag, out var version))
            return false;

        result = (channel, version);
        return true;
    }
}
