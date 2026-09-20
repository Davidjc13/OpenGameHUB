using System.Net;
using System.Text;
using OpenGameHUB.Infrastructure.Security;
using OpenGameHUB.Services.Configuration;
using OpenGameHUB.Services.Updates;

namespace OpenGameHUB.Tests;

public sealed class AppUpdateServiceTests
{
    [Fact]
    public void IsNewer_returns_false_for_empty_latest_tag()
    {
        Assert.False(AppUpdateService.IsNewer("", "alpha-0.0.10"));
    }

    [Fact]
    public void IsNewer_returns_true_in_dev_build_when_latest_tag_is_present()
    {
        Assert.True(AppUpdateService.IsDevBuild);
        Assert.True(AppUpdateService.IsNewer("alpha-0.0.10", "alpha-0.0.10"));
    }

    [Fact]
    public void BuildUpdateHelperBatch_runs_installer_and_relaunches_from_app_directory()
    {
        var installer = @"C:\Temp\OpenGameHUB-Setup-alpha-0.0.11.exe";
        var appExe = @"C:\Users\test\AppData\Local\Programs\OpenGameHUB\OpenGameHUB.exe";

        var batch = AppUpdateService.BuildUpdateHelperBatch(installer, appExe);

        Assert.Contains($"\"{installer}\" /SILENT /CLOSEAPPLICATIONS", batch);
        Assert.Contains($"if exist \"{appExe}\" start \"\" /D \"C:\\Users\\test\\AppData\\Local\\Programs\\OpenGameHUB\" \"{appExe}\"", batch);
    }

    [Fact]
    public void BuildDetachedLaunchArguments_starts_helper_detached()
    {
        var helper = @"C:\Temp\OpenGameHUB\updates\apply-update.cmd";

        var arguments = AppUpdateService.BuildDetachedLaunchArguments(helper);

        Assert.Equal($"/c start \"\" /MIN \"{helper}\"", arguments);
    }

    [Fact]
    public void TryMapRelease_includes_sha256_sidecar_url()
    {
        var release = new AppUpdateService.GitHubRelease
        {
            TagName = "alpha-0.0.12",
            HtmlUrl = "https://github.com/Davidjc13/OpenGameHUB/releases/tag/alpha-0.0.12",
            Assets =
            [
                new AppUpdateService.GitHubAsset
                {
                    Name = "OpenGameHUB-Setup-alpha-0.0.12.exe",
                    BrowserDownloadUrl = "https://example.test/OpenGameHUB-Setup-alpha-0.0.12.exe",
                    Size = 1024
                },
                new AppUpdateService.GitHubAsset
                {
                    Name = "OpenGameHUB-Setup-alpha-0.0.12.exe.sha256",
                    BrowserDownloadUrl = "https://example.test/OpenGameHUB-Setup-alpha-0.0.12.exe.sha256",
                    Size = 64
                }
            ]
        };

        var mapped = AppUpdateService.TryMapRelease(release);

        Assert.NotNull(mapped);
        Assert.Equal("https://example.test/OpenGameHUB-Setup-alpha-0.0.12.exe.sha256", mapped.ChecksumDownloadUrl);
    }

    [Fact]
    public void TryMapRelease_allows_installer_without_checksum_asset()
    {
        var release = new AppUpdateService.GitHubRelease
        {
            TagName = "alpha-0.0.12",
            Assets =
            [
                new AppUpdateService.GitHubAsset
                {
                    Name = "OpenGameHUB-Setup-alpha-0.0.12.exe",
                    BrowserDownloadUrl = "https://example.test/OpenGameHUB-Setup-alpha-0.0.12.exe",
                    Size = 1024
                }
            ]
        };

        var mapped = AppUpdateService.TryMapRelease(release);

        Assert.NotNull(mapped);
        Assert.Null(mapped.ChecksumDownloadUrl);
    }

    [Fact]
    public void VerifyInstallerChecksum_accepts_matching_hash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-setup-{Guid.NewGuid():N}.exe");
        File.WriteAllText(path, "installer-bytes");

        try
        {
            var hash = FileIntegrityVerifier.ComputeSha256Hex(path);
            AppUpdateService.VerifyInstallerChecksum(path, hash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VerifyInstallerChecksum_throws_on_mismatch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogh-setup-{Guid.NewGuid():N}.exe");
        File.WriteAllText(path, "installer-bytes");

        try
        {
            var ex = Assert.Throws<IntegrityVerificationException>(() =>
                AppUpdateService.VerifyInstallerChecksum(
                    path,
                    "A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456"));
            Assert.Equal(Loc.T("AppUpdateIntegrityFailed"), ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_requires_checksum_url()
    {
        var release = new AppReleaseInfo(
            "alpha-0.0.12",
            "https://example.test",
            "https://example.test/OpenGameHUB-Setup-alpha-0.0.12.exe",
            "OpenGameHUB-Setup-alpha-0.0.12.exe",
            12,
            null);

        using var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppUpdateService.DownloadInstallerAsync(http, release, null, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadInstallerAsync_verifies_sidecar_hash()
    {
        var payload = Encoding.UTF8.GetBytes("signed-installer");
        var path = Path.Combine(Path.GetTempPath(), $"ogh-hash-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(path, payload);
        var hash = FileIntegrityVerifier.ComputeSha256Hex(path);
        File.Delete(path);

        var assetName = $"OpenGameHUB-Setup-{Guid.NewGuid():N}.exe";
        var release = new AppReleaseInfo(
            "alpha-0.0.12",
            "https://example.test",
            "https://example.test/installer.exe",
            assetName,
            payload.Length,
            "https://example.test/installer.exe.sha256");

        using var handler = new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(hash)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        });
        using var http = new HttpClient(handler);

        var downloaded = await AppUpdateService.DownloadInstallerAsync(
            http,
            release,
            null,
            CancellationToken.None);

        try
        {
            Assert.True(File.Exists(downloaded));
            Assert.Equal(payload, await File.ReadAllBytesAsync(downloaded));
        }
        finally
        {
            File.Delete(downloaded);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_deletes_file_when_hash_mismatches()
    {
        var payload = Encoding.UTF8.GetBytes("tampered-installer");
        var assetName = $"OpenGameHUB-Setup-{Guid.NewGuid():N}.exe";
        var release = new AppReleaseInfo(
            "alpha-0.0.12",
            "https://example.test",
            "https://example.test/installer.exe",
            assetName,
            payload.Length,
            "https://example.test/installer.exe.sha256");

        using var handler = new FakeHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
        });
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<IntegrityVerificationException>(() =>
            AppUpdateService.DownloadInstallerAsync(http, release, null, CancellationToken.None));

        var leftover = Path.Combine(Path.GetTempPath(), "OpenGameHUB", "updates", assetName);
        Assert.False(File.Exists(leftover));
    }
}
