using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

public sealed class AppSettingsDocumentTests
{
    [Fact]
    public void From_and_ToSettings_round_trip_preferences()
    {
        var settings = new AppSettings
        {
            Language = "es",
            SteamId = "1",
            IgdbClientId = "igdb",
            CoverQualityMode = CoverQualityMode.High,
            UiFontScale = UiFontScale.ExtraLarge,
            EpicAccountId = "epic"
        };

        var secrets = new SettingsSecrets
        {
            SteamApiKey = "steam-key",
            IgdbClientSecret = "igdb-secret",
            SteamGridDbApiKey = "sgdb"
        };

        var document = AppSettingsDocument.From(settings);
        var roundTrip = document.ToSettings(secrets);

        Assert.Equal("es", roundTrip.Language);
        Assert.Equal("steam-key", roundTrip.SteamApiKey);
        Assert.Equal(CoverQualityMode.High, roundTrip.CoverQualityMode);
        Assert.Equal(UiFontScale.ExtraLarge, roundTrip.UiFontScale);
    }
}
