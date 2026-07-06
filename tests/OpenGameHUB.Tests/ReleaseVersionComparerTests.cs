using OpenGameHUB.Services.Updates;

namespace OpenGameHUB.Tests;

public sealed class ReleaseVersionComparerTests
{
    [Theory]
    [InlineData("alpha-0.0.10", "alpha-0.0.9-1", 1)]
    [InlineData("alpha-0.0.9-1", "alpha-0.0.10", -1)]
    [InlineData("alpha-0.0.10", "alpha-0.0.10", 0)]
    [InlineData("alpha-0.0.9-2", "alpha-0.0.9-1", 1)]
    [InlineData("alpha-0.0.9", "alpha-0.0.9-1", 1)]
    [InlineData("1.0.0", "alpha-0.9.9", 1)]
    [InlineData("beta-1.0.0", "alpha-9.9.9", 1)]
    public void Compare_orders_tags_correctly(string latest, string current, int expectedSign)
    {
        var result = Math.Sign(ReleaseVersionComparer.Compare(latest, current));
        Assert.Equal(expectedSign, result);
    }

    [Theory]
    [InlineData("not-a-version", "1.0.0", 1)]
    [InlineData("1.0.0", "not-a-version", -1)]
    public void Compare_falls_back_to_string_compare_for_unparsed_tags(string latest, string current, int expectedSign)
    {
        var result = Math.Sign(ReleaseVersionComparer.Compare(latest, current));
        Assert.Equal(expectedSign, result);
    }
}
