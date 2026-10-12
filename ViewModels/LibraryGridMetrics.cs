namespace OpenGameHUB.ViewModels;

public readonly record struct LibraryGridMetrics(
    int Columns,
    int Rows,
    double CardWidth,
    double CoverHeight,
    int PageSize,
    int RenderCount)
{
    public const double Gap = 12;
    public const double CardOuterMargin = 12;
    public const double MinCardWidth = 160;
    public const double MinPreferredCardWidth = 160;
    public const double DefaultPreferredCardWidth = 224;
    public const double MaxPreferredCardWidth = 320;
    public const double PreferredCardWidth = DefaultPreferredCardWidth;

    /// <summary>Border plus vertical margin around a grid cover. The title sits on the cover, so it does not add height.</summary>
    public const double CardChromeHeight = 20;

    public const double CoverAspect = 1.45;

    public static LibraryGridMetrics Calculate(
        double viewportWidth,
        double viewportHeight,
        double targetCardWidth = DefaultPreferredCardWidth)
    {
        var cardWidth = ClampPreferredCardWidth(targetCardWidth);
        var coverHeight = cardWidth * CoverAspect;
        var slotHeight = coverHeight + CardChromeHeight;

        if (viewportWidth < 1 || viewportHeight < 1)
            return new LibraryGridMetrics(3, 3, cardWidth, coverHeight, 9, 9);

        // Same width the wrap panel uses: the card's explicit width, with margin inside it.
        var columns = Math.Max(1, (int)Math.Floor((viewportWidth + 0.01) / cardWidth));
        var fullRows = FullRows(viewportHeight, slotHeight);
        var pageSize = columns * fullRows;
        var remainder = viewportHeight - fullRows * slotHeight;
        var renderCount = remainder > 8 ? pageSize + columns : pageSize;

        return new LibraryGridMetrics(columns, fullRows, cardWidth, coverHeight, pageSize, renderCount);
    }

    public static double ClampPreferredCardWidth(double preferredCardWidth) =>
        Math.Clamp(preferredCardWidth, MinPreferredCardWidth, MaxPreferredCardWidth);

    /// <summary>Rows that belong to the current page. A following row may be drawn as a clipped preview of the next page.</summary>
    public static int FullRows(double viewportHeight, double slotHeight)
    {
        if (viewportHeight < 1 || slotHeight < 1)
            return 1;

        return Math.Max(1, (int)Math.Floor((viewportHeight + 0.01) / slotHeight));
    }

    public static int ListPageSizeFromHeight(double viewportHeight)
    {
        const double rowHeight = 132;
        return Math.Max(8, (int)((viewportHeight + 8) / rowHeight));
    }
}
