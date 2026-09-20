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
}
