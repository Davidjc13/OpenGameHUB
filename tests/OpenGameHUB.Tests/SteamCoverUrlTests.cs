using OpenGameHUB.Providers.Steam;

namespace OpenGameHUB.Tests;

public sealed class SteamCoverUrlTests
{
    [Fact]
    public void GetCoverUrls_includes_library_capsule_and_hires_fallback()
    {
        var urls = SteamWebApiService.GetCoverUrls(570);

        Assert.Equal(2, urls.Count);
        Assert.EndsWith("/570/library_600x900.jpg", urls[0]);
        Assert.EndsWith("/570/library_600x900_2x.jpg", urls[1]);
    }

    [Fact]
    public void FindLocalLibraryCover_returns_cached_capsule()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ogh-steam-{Guid.NewGuid():N}"));
        var coverDir = Directory.CreateDirectory(Path.Combine(root.FullName, "appcache", "librarycache", "570"));
        var cover = Path.Combine(coverDir.FullName, "library_600x900.jpg");
        File.WriteAllText(cover, "image");

        try
        {
            Assert.Equal(cover, SteamWebApiService.FindLocalLibraryCover(570, root.FullName));
            Assert.Null(SteamWebApiService.FindLocalLibraryCover(730, root.FullName));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }

    [Fact]
    public void FindLocalLibraryCover_finds_library_capsule_in_subfolder()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ogh-steam-{Guid.NewGuid():N}"));
        var coverDir = Directory.CreateDirectory(Path.Combine(
            root.FullName,
            "appcache",
            "librarycache",
            "3764200",
            "ed3b2cae7d15f598f41006f5f1e605ec5517b5e4"));
        var cover = Path.Combine(coverDir.FullName, "library_capsule.jpg");
        File.WriteAllText(cover, "image");

        try
        {
            Assert.Equal(cover, SteamWebApiService.FindLocalLibraryCover(3764200, root.FullName));
        }
        finally
        {
            Directory.Delete(root.FullName, recursive: true);
        }
    }
}
