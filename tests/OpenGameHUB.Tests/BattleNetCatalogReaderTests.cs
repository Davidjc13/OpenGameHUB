using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.BattleNet;
using ProtoBuf;

namespace OpenGameHUB.Tests;

public sealed class BattleNetCatalogReaderTests
{
    [Theory]
    [InlineData("D3", "--game=diablo3 --install")]
    [InlineData("Hero", "--game=heroes --install")]
    [InlineData("WTCG", "--game=hs_beta --install")]
    [InlineData("hs_beta", "--game=hs_beta --install")]
    public void BuildInstallArguments_uses_internal_game_code(string productId, string expected)
    {
        Assert.Equal(expected, BattleNetCatalogReader.BuildInstallArguments(productId));
    }

    [Theory]
    [InlineData("WTCG", "--exec=\"launch WTCG\"")]
    [InlineData("hs_beta", "--exec=\"launch WTCG\"")]
    [InlineData("prometheus", "--exec=\"launch Pro\"")]
    public void BuildLaunchArguments_uses_official_product_code(string productId, string expected)
    {
        Assert.Equal(expected, BattleNetCatalogReader.BuildLaunchArguments(productId));
    }

    [Fact]
    public void BuildProductUri_uses_battlenet_protocol()
    {
        Assert.Equal("battlenet://Hero/", BattleNetCatalogReader.BuildProductUri("Hero"));
        Assert.Equal("--uri=battlenet://WTCG/", BattleNetCatalogReader.BuildUriArguments("WTCG"));
    }

    [Fact]
    public void FindByUid_resolves_prometheus_to_overwatch()
    {
        var game = BattleNetCatalogReader.FindByUid("prometheus");
        Assert.NotNull(game);
        Assert.Equal("Pro", game!.ProductId);
    }

    [Fact]
    public void FindByUid_resolves_regional_uid_suffix()
    {
        var game = BattleNetCatalogReader.FindByUid("wow_enus");
        Assert.NotNull(game);
        Assert.Equal("WoW", game!.ProductId);
    }

    [Fact]
    public void ReadLibraryEntriesFromProductDb_reads_serialized_database()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"ogh-bnet-{Guid.NewGuid():N}.db");
        try
        {
            var database = new BattleNetProductDatabase();
            database.ProductInstalls.Add(new BattleNetProductInstall
            {
                Uid = "wow",
                ProductCode = "wow",
                CachedProductState = new BattleNetCachedProductState
                {
                    BaseProductState = new BattleNetBaseProductState { Playable = true, Installed = true }
                },
                Settings = new BattleNetUserSettings { InstallPath = @"C:\Games\World of Warcraft" }
            });

            using (var stream = File.Create(tempDb))
                Serializer.Serialize(stream, database);

            BattleNetCatalogReader.ProductDbPathOverrideForTests = tempDb;
            var entries = BattleNetCatalogReader.ReadLibraryEntriesFromProductDb();

            Assert.Single(entries);
            Assert.Equal("WoW", entries[0].ProductId);
            Assert.True(entries[0].Installed);
        }
        finally
        {
            BattleNetCatalogReader.ProductDbPathOverrideForTests = null;
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    [Fact]
    public void ReadLibraryEntriesFromProductDb_includes_hearthstone_hs_beta_uid()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"ogh-bnet-{Guid.NewGuid():N}.db");
        try
        {
            var database = new BattleNetProductDatabase();
            database.ProductInstalls.Add(new BattleNetProductInstall
            {
                Uid = "hs_beta",
                ProductCode = "hsb",
                CachedProductState = new BattleNetCachedProductState
                {
                    BaseProductState = new BattleNetBaseProductState { Playable = true, Installed = true }
                }
            });

            using (var stream = File.Create(tempDb))
                Serializer.Serialize(stream, database);

            BattleNetCatalogReader.ProductDbPathOverrideForTests = tempDb;
            var entries = BattleNetCatalogReader.ReadLibraryEntriesFromProductDb();

            Assert.Single(entries);
            Assert.Equal("WTCG", entries[0].ProductId);
            Assert.Equal("Hearthstone", entries[0].Title);
        }
        finally
        {
            BattleNetCatalogReader.ProductDbPathOverrideForTests = null;
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    [Fact]
    public void ReadLibraryEntriesFromProductDb_includes_unmapped_uid_without_playable_flags()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"ogh-bnet-{Guid.NewGuid():N}.db");
        try
        {
            var database = new BattleNetProductDatabase();
            database.ProductInstalls.Add(new BattleNetProductInstall
            {
                Uid = "agent",
                ProductCode = "agent"
            });
            database.ProductInstalls.Add(new BattleNetProductInstall
            {
                Uid = "custom_game_uid",
                ProductCode = "CUST"
            });

            using (var stream = File.Create(tempDb))
                Serializer.Serialize(stream, database);

            BattleNetCatalogReader.ProductDbPathOverrideForTests = tempDb;
            var entries = BattleNetCatalogReader.ReadLibraryEntriesFromProductDb();

            Assert.Single(entries);
            Assert.Equal("CUST", entries[0].ProductId);
        }
        finally
        {
            BattleNetCatalogReader.ProductDbPathOverrideForTests = null;
            if (File.Exists(tempDb))
                File.Delete(tempDb);
        }
    }

    [Fact]
    public void ReadMergedCloudEntries_includes_hearthstone_and_heroes_when_launcher_present()
    {
        var previous = BattleNetCatalogReader.ProductDbPathOverrideForTests;
        try
        {
            BattleNetCatalogReader.ProductDbPathOverrideForTests =
                Path.Combine(Path.GetTempPath(), $"ogh-missing-{Guid.NewGuid():N}.db");

            var merged = BattleNetCatalogReader.ReadMergedCloudEntries();
            Assert.Contains(merged, entry => entry.ProductId == "WTCG" && entry.Title == "Hearthstone");
            Assert.Contains(merged, entry => entry.ProductId == "Hero" && entry.Title == "Heroes of the Storm");
            Assert.Contains(merged, entry => entry.ProductId == "S2" && entry.Title == "StarCraft II");
            Assert.DoesNotContain(merged, entry => entry.ProductId == "WoW");
            Assert.DoesNotContain(merged, entry => entry.ProductId == "ANBS");
        }
        finally
        {
            BattleNetCatalogReader.ProductDbPathOverrideForTests = previous;
        }
    }

    [Fact]
    public void MatchesInstalledGame_matches_product_id()
    {
        var unified = TestGames.Create("battlenet:1", Platform.BattleNet, "Overwatch 2", platformGameId: "Pro");
        var entry = new BattleNetCatalogEntry("prometheus", "Pro", "Overwatch 2", true, false, null);

        Assert.True(BattleNetCatalogReader.MatchesInstalledGame(unified, entry));
    }
}
