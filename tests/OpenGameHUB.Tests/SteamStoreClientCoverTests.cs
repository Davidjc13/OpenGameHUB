using OpenGameHUB.Providers.Steam;

namespace OpenGameHUB.Tests;

public sealed class SteamStoreClientCoverTests
{
    [Fact]
    public void NormalizeStoreAssetUrl_strips_query_and_upgrades_http()
    {
        var normalized = SteamStoreClient.NormalizeStoreAssetUrl(
            "http://shared.akamai.steamstatic.com/store_item_assets/steam/apps/3764200/header.jpg?t=123");

        Assert.Equal(
            "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/3764200/header.jpg",
            normalized);
    }
}
