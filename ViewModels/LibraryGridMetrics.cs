namespace OpenGameHUB.ViewModels;

public readonly record struct LibraryGridMetrics(
    int Columns,
    int Rows,
    double CardWidth,
    double CoverHeight,
    int PageSize)
{
    public const double Gap = 12;
    public const double CardOuterMargin = 12;
    public const double MinCardWidth = 160;
    public const double MinPreferredCardWidth = 160;
    public const double DefaultPreferredCardWidth = 224;
    public const double MaxPreferredCardWidth = 320;
    public const double PreferredCardWidth = DefaultPreferredCardWidth;
    public const double CardFooterHeight = 108;
    public const double CoverAspect = 1.45;

    public static LibraryGridMetrics Calculate(
        double viewportWidth,
        double viewportHeight,
        double targetCardWidth = DefaultPreferredCardWidth)
    {
        var cardWidth = ClampPreferredCardWidth(targetCardWidth);
        var coverHeight = cardWidth * CoverAspect;
        var slotWidth = cardWidth + CardOuterMargin;
        var slotHeight = coverHeight + CardFooterHeight + CardOuterMargin;

        if (viewportWidth < 1 || viewportHeight < 1)
            return new LibraryGridMetrics(3, 3, cardWidth, coverHeight, 9);

        var columns = Math.Max(1, (int)((viewportWidth + Gap) / (slotWidth + Gap)));
        var rows = Math.Max(1, (int)((viewportHeight + Gap) / (slotHeight + Gap)));

        return new LibraryGridMetrics(columns, rows, cardWidth, coverHeight, columns * rows);
    }

    public static double ClampPreferredCardWidth(double preferredCardWidth) =>
        Math.Clamp(preferredCardWidth, MinPreferredCardWidth, MaxPreferredCardWidth);

    public static int ListPageSizeFromHeight(double viewportHeight)
    {
        const double rowHeight = 92;
        return Math.Max(8, (int)((viewportHeight + 8) / rowHeight));
    }
}
