using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Ea;

namespace OpenGameHUB.Tests;

public sealed class EaCatalogReaderTests
{
    [Theory]
    [InlineData("battlefield-2042", true)]
    [InlineData("fifa-24", true)]
    [InlineData("550e8400-e29b-41d4-a716-446655440000", false)]
    [InlineData("Origin.SFT.123", false)]
    [InlineData("", false)]
    public void IsValidGameSlug_filters_invalid_values(string slug, bool expected)
    {
        Assert.Equal(expected, EaCatalogReader.IsValidGameSlug(slug));
    }

    [Theory]
    [InlineData("game-dlc", true)]
    [InlineData("game-addon", true)]
    [InlineData("battlefield-2042", false)]
    public void IsLikelyDlcOrAddon_detects_suffixes(string slug, bool expected)
    {
        Assert.Equal(expected, EaCatalogReader.IsLikelyDlcOrAddon(slug));
    }

    [Theory]
    [InlineData("fifa-24", "Fifa 24")]
    [InlineData("battlefield-2042", "Battlefield 2042")]
    [InlineData("nba-2k24", "NBA 2k24")]
    public void SlugToTitle_formats_display_names(string slug, string expected)
    {
        Assert.Equal(expected, EaCatalogReader.SlugToTitle(slug));
    }

    [Fact]
    public void PreferCatalogEntry_prefers_origin_sft_ids()
    {
        var origin = new EaCatalogEntry("Origin.SFT.123", "battlefield-2042", "Battlefield");
        var generic = new EaCatalogEntry("OFFER-1", "battlefield-2042", "Battlefield");

        var picked = EaCatalogReader.PreferCatalogEntry(origin, generic);
        Assert.Equal("Origin.SFT.123", picked.SoftwareId);
    }

    [Fact]
    public void MatchesInstalledGame_matches_by_software_id_or_title()
    {
        var entry = new EaCatalogEntry("OFFER-1", "fifa-24", "FIFA 24");
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ogh-ea-{Guid.NewGuid():N}")).FullName;

        try
        {
            var byId = TestGames.Create(
                "ea:installed:1",
                Platform.Ea,
                "Other",
                installed: true,
                installPath: dir,
                platformGameId: "OFFER-1");

            var byTitle = TestGames.Create(
                "ea:installed:2",
                Platform.Ea,
                "FIFA 24",
                installed: true,
                installPath: dir,
                platformGameId: "x");

            Assert.True(EaCatalogReader.MatchesInstalledGame(byId, entry));
            Assert.True(EaCatalogReader.MatchesInstalledGame(byTitle, entry));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
