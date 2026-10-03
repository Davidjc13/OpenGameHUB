using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class LibraryGridMetricsTests
{
    [Fact]
    public void Calculate_uses_target_card_width_directly()
    {
        var metrics = LibraryGridMetrics.Calculate(1200, 800, 200);

        Assert.Equal(200, metrics.CardWidth);
        Assert.Equal(200 * LibraryGridMetrics.CoverAspect, metrics.CoverHeight);
    }

    [Fact]
    public void Calculate_changes_card_width_continuously_within_same_column_count()
    {
        var smaller = LibraryGridMetrics.Calculate(1200, 800, 180);
        var larger = LibraryGridMetrics.Calculate(1200, 800, 195);

        Assert.Equal(180, smaller.CardWidth);
        Assert.Equal(195, larger.CardWidth);
        Assert.Equal(smaller.Columns, larger.Columns);
    }

    [Fact]
    public void Calculate_reduces_columns_when_viewport_is_narrow()
    {
        var metrics = LibraryGridMetrics.Calculate(360, 640, 224);

        Assert.Equal(1, metrics.Columns);
        Assert.Equal(224, metrics.CardWidth);
    }

    [Fact]
    public void Calculate_larger_cards_use_fewer_columns()
    {
        var compact = LibraryGridMetrics.Calculate(1200, 800, 160);
        var large = LibraryGridMetrics.Calculate(1200, 800, 320);

        Assert.True(large.Columns < compact.Columns);
        Assert.Equal(320, large.CardWidth);
    }

    [Fact]
    public void ClampPreferredCardWidth_clamps_to_bounds()
    {
        Assert.Equal(160, LibraryGridMetrics.ClampPreferredCardWidth(150));
        Assert.Equal(225, LibraryGridMetrics.ClampPreferredCardWidth(225));
        Assert.Equal(320, LibraryGridMetrics.ClampPreferredCardWidth(400));
    }
}
