using OpenGameHUB.Infrastructure.Security;
using OpenGameHUB.Providers.Epic;

namespace OpenGameHUB.Tests;

public sealed class LegendaryManifestTests
{
    [Fact]
    public void Current_loads_embedded_manifest_with_required_fields()
    {
        var manifest = LegendaryManifest.Current;

        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.StartsWith("https://github.com/legendary-gl/legendary/releases/download/", manifest.DownloadUrl);
        Assert.Equal(64, manifest.Sha256.Length);
        Assert.True(manifest.SizeBytes > 0);
    }

    [Fact]
    public void PromoteVerifiedDownload_moves_file_when_hash_matches()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogh-leg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var source = Path.Combine(tempDir, "legendary.exe.download");
        var destination = Path.Combine(tempDir, "legendary.exe");
        File.WriteAllText(source, "legendary-test-binary");

        try
        {
            var hash = FileIntegrityVerifier.ComputeSha256Hex(source);
            var manifest = new LegendaryManifest(
                "test",
                "https://example.test/legendary.exe",
                hash,
                new FileInfo(source).Length);

            LegendaryBootstrap.PromoteVerifiedDownload(source, destination, manifest);

            Assert.True(File.Exists(destination));
            Assert.False(File.Exists(source));
            Assert.Equal("legendary-test-binary", File.ReadAllText(destination));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PromoteVerifiedDownload_does_not_move_file_when_hash_mismatches()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogh-leg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var source = Path.Combine(tempDir, "legendary.exe.download");
        var destination = Path.Combine(tempDir, "legendary.exe");
        File.WriteAllText(source, "legendary-test-binary");

        try
        {
            var manifest = new LegendaryManifest(
                "test",
                "https://example.test/legendary.exe",
                "A1B2C3D4E5F6789012345678901234567890ABCDEF1234567890ABCDEF123456",
                new FileInfo(source).Length);

            Assert.Throws<IntegrityVerificationException>(() =>
                LegendaryBootstrap.PromoteVerifiedDownload(source, destination, manifest));

            Assert.True(File.Exists(source));
            Assert.False(File.Exists(destination));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
