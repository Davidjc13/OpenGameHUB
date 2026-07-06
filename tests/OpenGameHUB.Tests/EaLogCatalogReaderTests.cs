using OpenGameHUB.Providers.Ea;

namespace OpenGameHUB.Tests;

public sealed class EaLogCatalogReaderTests
{
    [Fact]
    public void ParseLibraryEntriesFromLogContent_parses_not_installed_games()
    {
        const string log = """
            [2024-01-15T10:00:00Z] IS update: set installInfo for softwareId=[OFFER-123] baseSlug=[battlefield-2042] installedStatus=[NotInstalled]
            [2024-01-16T10:00:00Z] IS update: set installInfo for softwareId=[OFFER-999] baseSlug=[fifa-24] installedStatus=[Installed]
            """;

        var entries = EaLogCatalogReader.ParseLibraryEntriesFromLogContent(log);

        Assert.Single(entries);
        Assert.Equal("battlefield-2042", entries[0].BaseSlug);
        Assert.Equal("Battlefield 2042", entries[0].Title);
    }

    [Fact]
    public void ParseLibraryEntriesFromLogContent_keeps_latest_timestamp_per_slug()
    {
        const string log = """
            [2024-01-15T10:00:00Z] IS update: set installInfo for softwareId=[OLD] baseSlug=[fifa-24] installedStatus=[NotInstalled]
            [2024-01-20T10:00:00Z] IS update: set installInfo for softwareId=[NEW] baseSlug=[fifa-24] installedStatus=[NotInstalled]
            """;

        var entries = EaLogCatalogReader.ParseLibraryEntriesFromLogContent(log);

        Assert.Single(entries);
        Assert.Equal("NEW", entries[0].SoftwareId);
    }

    [Fact]
    public void ParseLibraryEntriesFromLogContent_skips_dlc_slugs()
    {
        const string log = """
            [2024-01-15T10:00:00Z] IS update: set installInfo for softwareId=[OFFER-DLC] baseSlug=[game-dlc] installedStatus=[NotInstalled]
            """;

        Assert.Empty(EaLogCatalogReader.ParseLibraryEntriesFromLogContent(log));
    }
}
