namespace OpenGameHUB.ViewModels;

public readonly record struct LibraryGridMetrics(
    int Columns,
    int Rows,
    double CardWidth,
    double CoverHeight,
    int PageSize)
{
    public const double Gap = 12;
    public const double MinCardWidth = 136;
    public const double MaxCardWidth = 220;
    public const double CardFooterHeight = 108;
    public const double CoverAspect = 1.5;

    public static LibraryGridMetrics Calculate(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth < 1 || viewportHeight < 1)
            return new LibraryGridMetrics(4, 3, 180, 261, 12);

        var columns = Math.Max(1, (int)((viewportWidth + Gap) / (MinCardWidth + Gap)));
        var cardWidth = Math.Clamp(
            (viewportWidth - Gap * (columns - 1)) / columns,
            MinCardWidth,
            MaxCardWidth);

        var coverHeight = cardWidth * CoverAspect;
        var cardHeight = coverHeight + CardFooterHeight;
        var rows = Math.Max(1, (int)((viewportHeight + Gap) / (cardHeight + Gap)));

        return new LibraryGridMetrics(columns, rows, cardWidth, coverHeight, columns * rows);
    }

    public static int ListPageSizeFromHeight(double viewportHeight)
    {
        const double rowHeight = 92;
        return Math.Max(8, (int)((viewportHeight + 8) / rowHeight));
    }
}
