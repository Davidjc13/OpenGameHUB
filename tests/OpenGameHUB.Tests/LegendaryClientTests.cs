using OpenGameHUB.Providers.Epic;

namespace OpenGameHUB.Tests;

public sealed class LegendaryClientTests
{
    [Theory]
    [InlineData(@"C:\tools\legendary.exe", true)]
    [InlineData("", false)]
    public void IsLegendaryExecutable_detects_legendary_binary_name(string path, bool expected)
    {
        Assert.Equal(expected, LegendaryClient.IsLegendaryExecutable(path));
    }

    [Fact]
    public void BuildInstallProtocolUrl_encodes_catalog_identifiers()
    {
        var entry = new LegendaryCatalogEntry("Fortnite", "Fortnite", "fn", "itemId");

        Assert.Equal(
            "com.epicgames.launcher://apps/fn%3AitemId%3AFortnite?action=install",
            entry.BuildInstallProtocolUrl());
    }

    [Fact]
    public void BuildInstallProtocolUrl_rejects_unsafe_app_name()
    {
        var entry = new LegendaryCatalogEntry("Fortnite?action=uninstall", "Fortnite", "fn", "itemId");

        Assert.Null(entry.BuildInstallProtocolUrl());
    }
}
