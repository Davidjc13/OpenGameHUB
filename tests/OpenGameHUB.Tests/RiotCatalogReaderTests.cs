using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Riot;

namespace OpenGameHUB.Tests;

public sealed class RiotCatalogReaderTests
{
    [Fact]
    public void BuildLaunchArguments_adds_skip_to_install_flag_when_requested()
    {
        var args = RiotCatalogReader.BuildLaunchArguments("valorant", "live", install: true);

        Assert.Equal("--launch-product=valorant --launch-patchline=live --skip-to-install", args);
    }

    [Fact]
    public void TryGetLaunchProductFromSpec_parses_launcher_args_value()
    {
        var spec = LaunchSpec.LauncherArgs(
            @"C:\Riot Games\Riot Client\RiotClientServices.exe",
            "--launch-product=league_of_legends --launch-patchline=live");

        Assert.True(RiotCatalogReader.TryGetLaunchProductFromSpec(spec, out var productId));
        Assert.Equal("league_of_legends", productId);
    }

    [Fact]
    public void TryGetLaunchPatchlineFromSpec_parses_patchline_from_args()
    {
        var spec = LaunchSpec.LauncherArgs(
            @"C:\Riot Games\Riot Client\RiotClientServices.exe",
            "--launch-product=valorant --launch-patchline=pbe");

        Assert.True(RiotCatalogReader.TryGetLaunchPatchlineFromSpec(spec, out var patchline));
        Assert.Equal("pbe", patchline);
    }

    [Fact]
    public void MatchesInstalledGame_matches_by_product_id_or_title()
    {
        var game = TestGames.Create("riot:1", Platform.Riot, "VALORANT", platformGameId: "valorant");
        var entry = new RiotCatalogEntry("valorant", "VALORANT");

        Assert.True(RiotCatalogReader.MatchesInstalledGame(game, entry));
    }
}
