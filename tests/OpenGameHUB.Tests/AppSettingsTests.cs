using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;

namespace OpenGameHUB.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Configuration_flags_reflect_populated_fields()
    {
        var settings = new AppSettings
        {
            SteamApiKey = "key",
            SteamId = "76561198000000000",
            IgdbClientId = "id",
            IgdbClientSecret = "secret",
            SteamGridDbApiKey = "sgdb",
            EpicAccountId = "epic-1"
        };

        Assert.True(settings.IsSteamApiConfigured);
        Assert.True(settings.IsIgdbConfigured);
        Assert.True(settings.IsSteamGridDbConfigured);
        Assert.True(settings.IsCoverMetadataConfigured);
        Assert.True(settings.HasEpicAuth);
    }

    [Fact]
    public void Clone_creates_independent_copy()
    {
        var original = new AppSettings { Language = "es", SteamId = "1" };
        var clone = original.Clone();

        clone.Language = "en";
        Assert.Equal("es", original.Language);
    }
}
