using OpenGameHUB.Domain.Enums;
using OpenGameHUB.Domain.Models;
using OpenGameHUB.Providers.Ea;

namespace OpenGameHUB.Tests;

public sealed class EaAppUriBuilderTests
{
    [Fact]
    public void BuildOpenLibraryUrl_uses_slug_and_platform_query_parameters()
    {
        var url = EaAppUriBuilder.BuildOpenLibraryUrl("the-sims-4");

        Assert.Equal("link2ea://openlibrary?slug=the-sims-4&platform=EA", url);
    }

    [Fact]
    public void TryBuildOpenLibraryUrl_reads_slug_from_catalog_id()
    {
        var game = new UnifiedGame
        {
            Id = "ea:catalog:Origin.SFT.50.0002694@ea-sports-fc-25",
            Platform = Platform.Ea,
            PlatformGameId = "Origin.SFT.50.0002694",
            Title = "EA SPORTS FC 25",
            IsInstalled = false,
            LaunchSpec = LaunchSpec.Protocol("link2ea://openlibrary?slug=ea-sports-fc-25&platform=EA")
        };

        Assert.True(EaAppUriBuilder.TryBuildOpenLibraryUrl(game, out var url));
        Assert.Equal("link2ea://openlibrary?slug=ea-sports-fc-25&platform=EA", url);
    }

    [Fact]
    public void TryBuildOpenLibraryUrl_returns_false_without_slug()
    {
        var game = new UnifiedGame
        {
            Id = "ea:path:abc123",
            Platform = Platform.Ea,
            PlatformGameId = "Origin.SFT.50.0002694",
            Title = "Unknown",
            IsInstalled = false,
            LaunchSpec = LaunchSpec.Protocol("test://launch")
        };

        Assert.False(EaAppUriBuilder.TryBuildOpenLibraryUrl(game, out _));
    }
}
