using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxCatalogMergerTests
{
    [Fact]
    public void Merge_with_includeHistoryOnly_false_keeps_catalog_only()
    {
        var catalog = new[]
        {
            new XboxCatalogEntry { Pfn = "game.pfn", Title = "Catalog Game", StoreProductId = "A" }
        };
        var history = new[]
        {
            new XboxCatalogEntry
            {
                Pfn = "game.pfn",
                Title = "Catalog Game",
                PlaytimeMinutes = 120
            },
            new XboxCatalogEntry
            {
                Pfn = "history-only.pfn",
                Title = "History Only",
                PlaytimeMinutes = 30
            }
        };

        var merged = XboxCatalogMerger.Merge(catalog, history, includeHistoryOnly: false);

        Assert.Single(merged);
        Assert.Equal(120, merged[0].PlaytimeMinutes);
    }
}
