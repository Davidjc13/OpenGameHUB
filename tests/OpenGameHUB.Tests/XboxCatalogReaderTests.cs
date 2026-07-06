using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Xbox;

namespace OpenGameHUB.Tests;

public sealed class XboxCatalogReaderTests
{
    [Fact]
    public void BuildInstallLaunchSpec_uses_store_product_id_when_available()
    {
        var entry = new XboxCatalogEntry
        {
            Pfn = "PFN",
            Title = "Halo",
            StoreProductId = "StoreId123"
        };
        var spec = XboxCatalogReader.BuildInstallLaunchSpec(entry);

        Assert.Equal("protocol", spec.Kind);
        Assert.Equal("msxbox://game/?productId=StoreId123", spec.Value);
    }

    [Fact]
    public void MatchesInstalledGame_matches_by_pfn()
    {
        var installed = TestGames.Create("xbox:1", Platform.GamePass, "Halo", platformGameId: "Microsoft.Halo_8");
        var entry = new XboxCatalogEntry
        {
            Pfn = "Microsoft.Halo_8",
            Title = "Halo Infinite"
        };

        Assert.True(XboxCatalogReader.MatchesInstalledGame(installed, entry));
    }

    [Fact]
    public void BuildInstallLaunchSpec_falls_back_to_pfn_when_no_store_id()
    {
        var entry = new XboxCatalogEntry
        {
            Pfn = "Microsoft.Halo_8",
            Title = "Halo"
        };

        var spec = XboxCatalogReader.BuildInstallLaunchSpec(entry);
        Assert.Contains("PFN=", spec.Value);
    }
}
