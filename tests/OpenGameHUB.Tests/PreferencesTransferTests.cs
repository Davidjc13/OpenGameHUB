using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Services.Configuration;

namespace OpenGameHUB.Tests;

public sealed class PreferencesTransferTests
{
    [Fact]
    public void Serialize_writes_display_options_and_leaves_out_secrets_and_accounts()
    {
        var settings = new AppSettings
        {
            Language = "es",
            SteamApiKey = "secret-steam",
            SteamId = "76561198000000000",
            IgdbClientId = "igdb-id",
            IgdbClientSecret = "igdb-secret",
            SteamGridDbApiKey = "grid-key",
            CoverQualityMode = CoverQualityMode.High,
            UiFontScale = UiFontScale.Large,
            ThemeMode = ThemeMode.Dark,
            LibraryViewMode = LibraryViewMode.List,
            GridCardSize = 240,
            PageSlideEnabled = true,
            DismissSteamApiKeyPrompt = true,
            EpicAccountId = "epic-id",
            EpicDisplayName = "EpicName",
            XboxGamertag = "Gamertag"
        };

        var json = PreferencesTransfer.Serialize(settings);

        Assert.Contains("\"Format\": \"opengamehub-preferences\"", json);
        Assert.Contains("\"Language\": \"es\"", json);
        Assert.Contains("\"CoverQualityMode\": \"High\"", json);
        Assert.Contains("\"UiFontScale\": \"Large\"", json);
        Assert.Contains("\"ThemeMode\": \"Dark\"", json);
        Assert.Contains("\"LibraryViewMode\": \"List\"", json);
        Assert.Contains("\"PageSlideEnabled\": true", json);
        Assert.DoesNotContain("secret-steam", json);
        Assert.DoesNotContain("76561198000000000", json);
        Assert.DoesNotContain("igdb-id", json);
        Assert.DoesNotContain("igdb-secret", json);
        Assert.DoesNotContain("grid-key", json);
        Assert.DoesNotContain("epic-id", json);
        Assert.DoesNotContain("EpicName", json);
        Assert.DoesNotContain("Gamertag", json);
        Assert.DoesNotContain("SteamApiKey", json);
        Assert.DoesNotContain("EpicAccountId", json);
        Assert.DoesNotContain("XboxGamertag", json);
    }

    [Fact]
    public void Serialize_keeps_collection_names_and_drops_manual_games()
    {
        var json = PreferencesTransfer.Serialize(new AppSettings(),
        [
            new PortableCollection
            {
                Name = "Backlog",
                SortOrder = 1,
                GameIds = ["steam:570", "custom:path:abc", "epic:legendary:Game"]
            }
        ]);

        Assert.Contains("\"Name\": \"Backlog\"", json);
        Assert.Contains("steam:570", json);
        Assert.Contains("epic:legendary:Game", json);
        Assert.DoesNotContain("custom:path:abc", json);
    }

    [Fact]
    public void Serialize_writes_favorites_and_drops_manual_games()
    {
        var json = PreferencesTransfer.Serialize(
            new AppSettings(),
            collections: null,
            favoriteGameIds: ["steam:570", "custom:path:abc", "epic:legendary:Game"]);

        Assert.Contains("steam:570", json);
        Assert.Contains("epic:legendary:Game", json);
        Assert.DoesNotContain("custom:path:abc", json);
    }

    [Fact]
    public void ReadFavorites_returns_null_when_the_file_has_no_favorites()
    {
        var json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "Language": "es"
            }
            """;

        Assert.Null(PreferencesTransfer.ReadFavorites(json));
    }

    [Fact]
    public void ReadFavorites_drops_manual_games()
    {
        var json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "FavoriteGameIds": ["steam:10", "custom:path:abc"]
            }
            """;

        var favorites = PreferencesTransfer.ReadFavorites(json);

        Assert.NotNull(favorites);
        Assert.Equal(["steam:10"], favorites);
    }

    [Fact]
    public void ReadCollections_returns_null_when_the_file_has_no_collections()
    {
        var json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "Language": "es"
            }
            """;

        Assert.Null(PreferencesTransfer.ReadCollections(json));
    }

    [Fact]
    public void ReadCollections_drops_manual_games()
    {
        var json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "Collections": [
                {
                  "Name": "RPG",
                  "SortOrder": 0,
                  "GameIds": ["steam:10", "custom:path:abc"]
                }
              ]
            }
            """;

        var collections = PreferencesTransfer.ReadCollections(json);

        Assert.NotNull(collections);
        var collection = Assert.Single(collections);
        Assert.Equal("RPG", collection.Name);
        Assert.Equal(["steam:10"], collection.GameIds);
    }

    [Fact]
    public void Apply_replaces_options_and_keeps_secrets_and_accounts()
    {
        var current = new AppSettings
        {
            Language = "en",
            SteamApiKey = "local-steam",
            SteamId = "local-id",
            IgdbClientId = "local-igdb",
            IgdbClientSecret = "local-secret",
            SteamGridDbApiKey = "local-grid",
            CoverQualityMode = CoverQualityMode.Low,
            ThemeMode = ThemeMode.System,
            DismissLegendaryPrompt = true,
            EpicAccountId = "local-epic",
            EpicDisplayName = "LocalEpic",
            XboxGamertag = "LocalTag"
        };

        var json = PreferencesTransfer.Serialize(new AppSettings
        {
            Language = "es",
            SteamApiKey = "other-pc-key",
            CoverQualityMode = CoverQualityMode.None,
            UiFontScale = UiFontScale.ExtraSmall,
            ThemeMode = ThemeMode.Light,
            LibraryViewMode = LibraryViewMode.List,
            GridCardSize = 180,
            PageSlideEnabled = true
        });

        var applied = PreferencesTransfer.Apply(current, json);

        Assert.Equal("es", applied.Language);
        Assert.Equal(CoverQualityMode.None, applied.CoverQualityMode);
        Assert.Equal(UiFontScale.ExtraSmall, applied.UiFontScale);
        Assert.Equal(ThemeMode.Light, applied.ThemeMode);
        Assert.Equal(LibraryViewMode.List, applied.LibraryViewMode);
        Assert.Equal(180, applied.GridCardSize);
        Assert.True(applied.PageSlideEnabled);
        Assert.Equal("local-steam", applied.SteamApiKey);
        Assert.Equal("local-id", applied.SteamId);
        Assert.Equal("local-igdb", applied.IgdbClientId);
        Assert.Equal("local-secret", applied.IgdbClientSecret);
        Assert.Equal("local-grid", applied.SteamGridDbApiKey);
        Assert.True(applied.DismissLegendaryPrompt);
        Assert.Equal("local-epic", applied.EpicAccountId);
        Assert.Equal("LocalEpic", applied.EpicDisplayName);
        Assert.Equal("LocalTag", applied.XboxGamertag);
    }

    [Fact]
    public void Apply_ignores_secret_fields_even_if_the_file_contains_them()
    {
        const string json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "Language": "es",
              "SteamApiKey": "pasted-key",
              "SteamId": "pasted-id",
              "IgdbClientSecret": "pasted-secret",
              "EpicAccountId": "pasted-epic",
              "XboxGamertag": "PastedTag"
            }
            """;

        var applied = PreferencesTransfer.Apply(new AppSettings
        {
            SteamApiKey = "kept-key",
            SteamId = "kept-id",
            IgdbClientSecret = "kept-secret",
            EpicAccountId = "kept-epic",
            XboxGamertag = "KeptTag"
        }, json);

        Assert.Equal("es", applied.Language);
        Assert.Equal("kept-key", applied.SteamApiKey);
        Assert.Equal("kept-id", applied.SteamId);
        Assert.Equal("kept-secret", applied.IgdbClientSecret);
        Assert.Equal("kept-epic", applied.EpicAccountId);
        Assert.Equal("KeptTag", applied.XboxGamertag);
    }

    [Fact]
    public void Apply_keeps_current_options_when_the_file_omits_them()
    {
        const string json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "ThemeMode": "Dark"
            }
            """;

        var applied = PreferencesTransfer.Apply(new AppSettings
        {
            Language = "es",
            CoverQualityMode = CoverQualityMode.High,
            PageSlideEnabled = true
        }, json);

        Assert.Equal("es", applied.Language);
        Assert.Equal(CoverQualityMode.High, applied.CoverQualityMode);
        Assert.Equal(ThemeMode.Dark, applied.ThemeMode);
        Assert.True(applied.PageSlideEnabled);
    }

    [Fact]
    public void Apply_clamps_grid_card_size()
    {
        const string json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 1,
              "GridCardSize": 900
            }
            """;

        var applied = PreferencesTransfer.Apply(new AppSettings(), json);

        Assert.Equal(320, applied.GridCardSize);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"Language":"es"}""")]
    [InlineData("""{"Format":"other","Version":1}""")]
    [InlineData("""{"Format":"opengamehub-preferences","Version":1,"Language":"fr"}""")]
    [InlineData("""{"Format":"opengamehub-preferences","Version":1,"ThemeMode":"Purple"}""")]
    public void Apply_rejects_files_that_are_not_preferences(string json)
    {
        var error = Assert.Throws<PreferencesTransferException>(() =>
            PreferencesTransfer.Apply(new AppSettings(), json));

        Assert.Equal(PreferencesTransferFailure.InvalidFile, error.Failure);
    }

    [Fact]
    public void Apply_rejects_a_newer_file_version()
    {
        const string json = """
            {
              "Format": "opengamehub-preferences",
              "Version": 2,
              "Language": "es"
            }
            """;

        var error = Assert.Throws<PreferencesTransferException>(() =>
            PreferencesTransfer.Apply(new AppSettings(), json));

        Assert.Equal(PreferencesTransferFailure.NewerVersion, error.Failure);
    }
}
