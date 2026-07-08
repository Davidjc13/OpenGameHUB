using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxCloudLibraryProviderTests
{
    [Fact]
    public async Task LoadLibraryAsync_keeps_catalog_when_history_enrichment_fails()
    {
        var catalog = new[]
        {
            new XboxCatalogEntry
            {
                Pfn = "publisher.game_pfn",
                Title = "Catalog Game",
                StoreProductId = "9PK09BL31FK1"
            }
        };

        var provider = new XboxCloudLibraryProvider(
            getCatalogAsync: _ => Task.FromResult<IReadOnlyList<XboxCatalogEntry>>(catalog),
            getHistoryAsync: _ => throw new InvalidOperationException("history unavailable"),
            isAvailable: () => true);

        await provider.LoadLibraryAsync();

        var uninstalled = provider.GetUninstalledLibraryGames([]);

        Assert.Single(uninstalled);
        Assert.Equal("Catalog Game", uninstalled[0].Title);
        Assert.Equal(Platform.GamePass, uninstalled[0].Platform);
        Assert.False(uninstalled[0].IsInstalled);
    }

    [Fact]
    public async Task LoadLibraryAsync_enriches_catalog_with_history_playtime()
    {
        var catalog = new[]
        {
            new XboxCatalogEntry
            {
                Pfn = "publisher.game_pfn",
                Title = "Catalog Game",
                StoreProductId = "9PK09BL31FK1"
            }
        };
        var history = new[]
        {
            new XboxCatalogEntry
            {
                Pfn = "publisher.game_pfn",
                Title = "Catalog Game",
                PlaytimeMinutes = 240
            },
            new XboxCatalogEntry
            {
                Pfn = "history-only.pfn",
                Title = "History Only",
                PlaytimeMinutes = 30
            }
        };

        var provider = new XboxCloudLibraryProvider(
            getCatalogAsync: _ => Task.FromResult<IReadOnlyList<XboxCatalogEntry>>(catalog),
            getHistoryAsync: _ => Task.FromResult<IReadOnlyList<XboxCatalogEntry>>(history),
            isAvailable: () => true);

        await provider.LoadLibraryAsync();

        var uninstalled = provider.GetUninstalledLibraryGames([]);

        Assert.Single(uninstalled);
        Assert.Equal(240, uninstalled[0].PlaytimeMinutes);
    }
}
