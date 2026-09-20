using OpenGameHUB.Infrastructure;

namespace OpenGameHUB.Tests;

public sealed class ProtocolUriTests
{
    [Theory]
    [InlineData("Fortnite")]
    [InlineData("Origin.SFT.50.0002694")]
    [InlineData("Microsoft.Halo_8wekyb3d8bbwe")]
    [InlineData("gog_1207658924")]
    [InlineData("9NTL0QDWZ4FS")]
    [InlineData("the-sims-4")]
    public void IsCatalogToken_accepts_platform_identifiers(string value)
    {
        Assert.True(ProtocolUri.IsCatalogToken(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("foo/bar")]
    [InlineData("foo?action=evil")]
    [InlineData("foo&calc")]
    [InlineData("foo|calc")]
    [InlineData("foo bar")]
    [InlineData("steam://install/1")]
    [InlineData("foo\nbar")]
    [InlineData("foo%3Abar")]
    [InlineData("..")]
    [InlineData("foo..bar")]
    public void IsCatalogToken_rejects_injection_payloads(string? value)
    {
        Assert.False(ProtocolUri.IsCatalogToken(value));
    }

    [Fact]
    public void IsCatalogToken_rejects_overlong_values()
    {
        Assert.False(ProtocolUri.IsCatalogToken(new string('a', ProtocolUri.MaxCatalogTokenLength + 1)));
        Assert.True(ProtocolUri.IsCatalogToken(new string('a', ProtocolUri.MaxCatalogTokenLength)));
    }

    [Fact]
    public void TrySteamInstall_builds_install_url()
    {
        Assert.True(ProtocolUri.TrySteamInstall(570, out var url));
        Assert.Equal("steam://install/570", url);
        Assert.True(ProtocolUri.IsLaunchable(url));
    }

    [Fact]
    public void TrySteamStoreAndUninstall_build_product_urls()
    {
        Assert.True(ProtocolUri.TrySteamStore(570, out var storeUrl));
        Assert.Equal("steam://store/570", storeUrl);
        Assert.True(ProtocolUri.IsLaunchable(storeUrl));
        Assert.True(ProtocolUri.TrySteamUninstall(570, out var uninstallUrl));
        Assert.Equal("steam://uninstall/570", uninstallUrl);
        Assert.True(ProtocolUri.IsLaunchable(uninstallUrl));
        Assert.False(ProtocolUri.TrySteamStore(0, out _));
        Assert.False(ProtocolUri.TrySteamUninstall(-1, out _));
    }

    [Fact]
    public void TrySteamInstall_rejects_non_positive_app_id()
    {
        Assert.False(ProtocolUri.TrySteamInstall(0, out _));
        Assert.False(ProtocolUri.TrySteamInstall(-1, out _));
    }

    [Fact]
    public void TryEpicInstall_builds_app_url()
    {
        Assert.True(ProtocolUri.TryEpicInstall("Fortnite", out var url));
        Assert.Equal("com.epicgames.launcher://apps/Fortnite?action=install", url);
        Assert.True(ProtocolUri.IsLaunchable(url));
    }

    [Fact]
    public void TryEpicCatalogInstall_encodes_colon_separators()
    {
        Assert.True(ProtocolUri.TryEpicCatalogInstall("fn", "itemId", "Fortnite", out var url));
        Assert.Equal("com.epicgames.launcher://apps/fn%3AitemId%3AFortnite?action=install", url);
        Assert.True(ProtocolUri.IsLaunchable(url));
    }

    [Fact]
    public void TryEpicCatalogInstall_rejects_unsafe_component()
    {
        Assert.False(ProtocolUri.TryEpicCatalogInstall("fn", "item/../x", "Fortnite", out _));
        Assert.False(ProtocolUri.TryEpicInstall("Fortnite?action=uninstall", out _));
    }

    [Fact]
    public void TryEpicLaunch_adds_silent_query()
    {
        Assert.True(ProtocolUri.TryEpicLaunch("Fortnite", out var url));
        Assert.Equal("com.epicgames.launcher://apps/Fortnite?action=launch&silent=true", url);
        Assert.True(ProtocolUri.IsLaunchable(url));
    }

    [Fact]
    public void TryEpicStoreAndUninstall_build_product_urls()
    {
        Assert.True(ProtocolUri.TryEpicStore("Fortnite", out var storeUrl));
        Assert.Equal("com.epicgames.launcher://store/product/Fortnite", storeUrl);
        Assert.True(ProtocolUri.IsLaunchable(storeUrl));
        Assert.True(ProtocolUri.TryEpicUninstall("Fortnite", out var uninstallUrl));
        Assert.Equal("com.epicgames.launcher://apps/Fortnite?action=uninstall", uninstallUrl);
        Assert.True(ProtocolUri.IsLaunchable(uninstallUrl));
        Assert.False(ProtocolUri.TryEpicStore("Fortnite?action=evil", out _));
    }

    [Fact]
    public void TryUplayInstall_requires_positive_id()
    {
        Assert.True(ProtocolUri.TryUplayInstall(12345u, out var url));
        Assert.Equal("uplay://install/12345", url);
        Assert.False(ProtocolUri.TryUplayInstall("not-a-number", out _));
        Assert.False(ProtocolUri.TryUplayInstall(0u, out _));
        Assert.True(ProtocolUri.TryUplayUninstall(12345u, out var uninstallUrl));
        Assert.Equal("uplay://uninstall/12345", uninstallUrl);
        Assert.False(ProtocolUri.TryUplayUninstall("nope", out _));
    }

    [Fact]
    public void TryGogOpenGameView_builds_galaxy_url()
    {
        Assert.True(ProtocolUri.TryGogOpenGameView("gog_42", out var url));
        Assert.Equal("goggalaxy://openGameView/gog_42", url);
        Assert.True(ProtocolUri.IsLaunchable(url));
    }

    [Fact]
    public void TryEaAndOriginLaunch_use_content_id()
    {
        const string contentId = "Origin.SFT.50.0002694";
        Assert.True(ProtocolUri.TryEaLaunch(contentId, out var eaUrl));
        Assert.Equal($"link2ea://launchgame/contentids/{contentId}", eaUrl);
        Assert.True(ProtocolUri.TryOriginLaunch(contentId, out var originUrl));
        Assert.Equal($"origin2://game/launch?offerIds={contentId}", originUrl);
        Assert.True(ProtocolUri.IsLaunchable(eaUrl));
        Assert.True(ProtocolUri.IsLaunchable(originUrl));
    }

    [Fact]
    public void TryXboxAndStore_escape_safe_tokens()
    {
        Assert.True(ProtocolUri.TryXboxProduct("9NTL0QDWZ4FS", out var xboxUrl));
        Assert.Equal("msxbox://game/?productId=9NTL0QDWZ4FS", xboxUrl);
        Assert.True(ProtocolUri.TryStoreProduct("9NTL0QDWZ4FS", out var storeUrl));
        Assert.Equal("ms-windows-store://pdp/?ProductId=9NTL0QDWZ4FS", storeUrl);
        Assert.True(ProtocolUri.TryStorePfn("Microsoft.Halo_8", out var pfnUrl));
        Assert.Equal("ms-windows-store://pdp/?PFN=Microsoft.Halo_8", pfnUrl);
        Assert.True(ProtocolUri.IsLaunchable(xboxUrl));
        Assert.True(ProtocolUri.IsLaunchable(storeUrl));
        Assert.True(ProtocolUri.IsLaunchable(pfnUrl));
        Assert.True(ProtocolUri.IsLaunchable(ProtocolUri.StoreGamingPage));
    }

    [Theory]
    [InlineData("steam://install/570")]
    [InlineData("steam://store/570")]
    [InlineData("steam://uninstall/570")]
    [InlineData("com.epicgames.launcher://store/product/Fortnite")]
    [InlineData("com.epicgames.launcher://apps/Fortnite?action=uninstall")]
    [InlineData("com.epicgames.launcher://apps/Fortnite?action=launch&silent=true")]
    [InlineData("link2ea://openlibrary?slug=the-sims-4&platform=EA")]
    [InlineData("shell:AppsFolder\\Microsoft.Halo_8wekyb3d8bbwe!App")]
    [InlineData("shell:AppsFolder/Microsoft.Halo_8wekyb3d8bbwe!App")]
    public void IsLaunchable_accepts_known_launcher_protocols(string url)
    {
        Assert.True(ProtocolUri.IsLaunchable(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-msdt:something")]
    [InlineData("steam://install/570&calc")]
    [InlineData("steam://install/570\ncalc")]
    [InlineData("shell:startup")]
    [InlineData("shell:AppsFolder\\Microsoft.Halo_8!App&calc")]
    public void IsLaunchable_rejects_non_launcher_or_injected_urls(string? url)
    {
        Assert.False(ProtocolUri.IsLaunchable(url));
    }

    [Fact]
    public void EnsureLaunchable_throws_for_rejected_url()
    {
        Assert.Throws<InvalidOperationException>(() => ProtocolUri.EnsureLaunchable("file:///C:/Windows/notepad.exe"));
    }

    [Fact]
    public void IsLaunchable_rejects_overlong_urls()
    {
        Assert.False(ProtocolUri.IsLaunchable("steam://install/" + new string('1', ProtocolUri.MaxUrlLength)));
    }
}
