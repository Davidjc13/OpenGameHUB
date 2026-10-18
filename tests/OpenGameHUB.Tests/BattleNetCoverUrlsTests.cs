using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.BattleNet;
using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class BattleNetCoverUrlsTests
{
    [Fact]
    public void GetOfficialCoverUrl_returns_blizzard_card_for_known_product()
    {
        var url = BattleNetCoverUrls.GetOfficialCoverUrl("ODIN", "Call of Duty: Modern Warfare");

        Assert.NotNull(url);
        Assert.Contains("bnetproduct-a.akamaihd.net", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoverUrls_returns_igdb_for_product_without_official_card()
    {
        var urls = BattleNetCoverUrls.GetCoverUrls("Fen", "Diablo IV");

        Assert.Single(urls);
        Assert.Contains("images.igdb.com", urls[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("co1sh6", urls[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoverUrls_prefers_title_when_product_id_is_shared()
    {
        var remastered = BattleNetCoverUrls.GetCoverUrls("S1", "StarCraft Remastered");
        var original = BattleNetCoverUrls.GetCoverUrls("S1", "StarCraft");

        Assert.Contains("co3wal", Assert.Single(remastered), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("co1x7n", Assert.Single(original), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoverUrls_resolves_internal_id()
    {
        var urls = BattleNetCoverUrls.GetCoverUrls("fenris", null);

        Assert.Contains("co1sh6", Assert.Single(urls), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCoverUrls_returns_empty_when_catalog_has_no_art()
    {
        var urls = BattleNetCoverUrls.GetCoverUrls("Hero", "Heroes of the Storm");

        Assert.Empty(urls);
    }

    [Fact]
    public async Task FindCoverUrlsAsync_puts_official_card_before_shared_fallbacks()
    {
        var client = new BattleNetCoverClient();
        var game = new UnifiedGame
        {
            Id = "battlenet:catalog:W3",
            Platform = Platform.BattleNet,
            PlatformGameId = "W3",
            Title = "Warcraft III: Reforged",
            LaunchSpec = LaunchSpec.None
        };

        var urls = await client.FindCoverUrlsAsync(game);

        Assert.Contains("bnetproduct-a.akamaihd.net", Assert.Single(urls), StringComparison.OrdinalIgnoreCase);
    }
}
