using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Infrastructure;
using OpenGameHUB.Services.Games;

namespace OpenGameHUB.Tests;

public sealed class GameLibraryActionsTests
{
    [Fact]
    public void CanOpenInstallFolder_requires_existing_directory()
    {
        var missing = TestGames.Create(
            "steam:1",
            Platform.Steam,
            "Missing",
            installed: true,
            installPath: @"C:\does-not-exist-ogh-folder",
            platformGameId: "570");
        Assert.False(GameLibraryActions.CanOpenInstallFolder(missing));

        var dir = Directory.CreateTempSubdirectory("ogh-install-");
        try
        {
            var installed = TestGames.Create(
                "steam:2",
                Platform.Steam,
                "Installed",
                installed: true,
                installPath: dir.FullName,
                platformGameId: "570");
            Assert.True(GameLibraryActions.TryGetInstallFolder(installed, out var path));
            Assert.Equal(Path.GetFullPath(dir.FullName), path);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void TryGetStoreUrl_builds_steam_epic_and_gog_product_links()
    {
        var steam = TestGames.Create("steam:store:570", Platform.Steam, "Dota", platformGameId: "570");
        Assert.True(GameLibraryActions.TryGetStoreUrl(steam, out var steamUrl));
        Assert.Equal("steam://store/570", steamUrl);
        Assert.True(ProtocolUri.IsLaunchable(steamUrl));

        var epic = TestGames.Create("epic:legendary:Fortnite", Platform.Epic, "Fortnite", platformGameId: "Fortnite");
        Assert.True(GameLibraryActions.TryGetStoreUrl(epic, out var epicUrl));
        Assert.Equal("com.epicgames.launcher://store/product/Fortnite", epicUrl);
        Assert.True(ProtocolUri.IsLaunchable(epicUrl));

        var gog = TestGames.Create(
            "gog:catalog:1207658924@gog_1207658924",
            Platform.Gog,
            "Witcher",
            platformGameId: "1207658924");
        Assert.True(GameLibraryActions.TryGetStoreUrl(gog, out var gogUrl));
        Assert.Equal("goggalaxy://openGameView/gog_1207658924", gogUrl);
        Assert.True(ProtocolUri.IsLaunchable(gogUrl));
    }

    [Fact]
    public void TryGetStoreUrl_uses_gog_id_when_catalog_key_is_missing()
    {
        var gog = TestGames.Create("gog:path:abcd", Platform.Gog, "Witcher", platformGameId: "42");
        Assert.True(GameLibraryActions.TryGetStoreUrl(gog, out var url));
        Assert.Equal("goggalaxy://openGameView/gog_42", url);
    }

    [Theory]
    [InlineData(Platform.Custom, "custom:path:1", "game.exe")]
    [InlineData(Platform.Ea, "ea:catalog:1", "origin-id")]
    [InlineData(Platform.Steam, "steam:1", "not-a-number")]
    public void TryGetStoreUrl_rejects_games_without_a_product_page(Platform platform, string id, string platformGameId)
    {
        var game = TestGames.Create(id, platform, "Game", platformGameId: platformGameId);
        Assert.False(GameLibraryActions.CanViewInStore(game));
    }

    [Fact]
    public void TryGetUninstallSpec_uses_launcher_protocols_for_installed_store_games()
    {
        var steam = TestGames.Create(
            "steam:path:1",
            Platform.Steam,
            "Dota",
            installed: true,
            platformGameId: "570");
        Assert.True(GameLibraryActions.TryGetUninstallSpec(steam, out var steamSpec));
        Assert.Equal("protocol", steamSpec.Kind);
        Assert.Equal("steam://uninstall/570", steamSpec.Value);

        var epic = TestGames.Create(
            "epic:manifest:Fortnite",
            Platform.Epic,
            "Fortnite",
            installed: true,
            platformGameId: "Fortnite");
        Assert.True(GameLibraryActions.TryGetUninstallSpec(epic, out var epicSpec));
        Assert.Equal("com.epicgames.launcher://apps/Fortnite?action=uninstall", epicSpec.Value);

        var ubisoft = TestGames.Create(
            "ubisoft:1",
            Platform.Ubisoft,
            "Game",
            installed: true,
            platformGameId: "12345");
        Assert.True(GameLibraryActions.TryGetUninstallSpec(ubisoft, out var uplaySpec));
        Assert.Equal("uplay://uninstall/12345", uplaySpec.Value);
    }

    [Fact]
    public void CanUninstall_is_false_for_uninstalled_or_custom_games()
    {
        var cloud = TestGames.Create("steam:store:570", Platform.Steam, "Dota", platformGameId: "570");
        Assert.False(GameLibraryActions.CanUninstall(cloud));

        var custom = TestGames.Create(
            "custom:path:abcd",
            Platform.Custom,
            "Indie",
            installed: true,
            installPath: @"C:\Games\Indie",
            platformGameId: "game.exe");
        Assert.False(GameLibraryActions.CanUninstall(custom));
        Assert.True(GameLibraryActions.CanRemoveFromLibrary(custom));
        Assert.True(GameLibraryActions.IsCustom(custom));
    }
}
