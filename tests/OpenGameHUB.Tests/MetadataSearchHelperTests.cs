using OpenGameHUB.Services.Covers;

namespace OpenGameHUB.Tests;

public sealed class MetadataSearchHelperTests
{
    [Theory]
    [InlineData("Counter-Strike 2™", "Counter-Strike 2")]
    [InlineData("Halo Infinite - PC", "Halo Infinite")]
    [InlineData("Sea of Thieves (Windows)", "Sea of Thieves")]
    public void NormalizeTitle_strips_store_suffixes_and_symbols(string input, string expected)
    {
        Assert.Equal(expected, MetadataSearchHelper.NormalizeTitle(input));
    }
}
