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
}
