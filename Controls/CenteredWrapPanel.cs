using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace OpenGameHUB.Controls;

public class CenteredWrapPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : availableSize.Width;

        var x = 0d;
        var y = 0d;
        var rowHeight = 0d;
        var maxRight = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(maxWidth, availableSize.Height));
            var size = child.DesiredSize;

            if (x + size.Width > maxWidth && x > 0)
            {
                y += rowHeight;
                x = 0;
                rowHeight = 0;
            }

            x += size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
            maxRight = Math.Max(maxRight, x);
        }

        var totalHeight = y + rowHeight;
        var width = double.IsInfinity(availableSize.Width) ? maxRight : availableSize.Width;
        return new Size(width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rows = new List<List<Control>>();
        var currentRow = new List<Control>();
        var rowWidth = 0d;

        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (rowWidth + size.Width > finalSize.Width + 0.01 && currentRow.Count > 0)
            {
                rows.Add(currentRow);
                currentRow = [];
                rowWidth = 0;
            }

            currentRow.Add(child);
            rowWidth += size.Width;
        }

        if (currentRow.Count > 0)
            rows.Add(currentRow);

        var y = 0d;
        foreach (var row in rows)
        {
            var height = row.Max(item => item.DesiredSize.Height);
            var width = row.Sum(item => item.DesiredSize.Width);
            var x = Math.Max(0, (finalSize.Width - width) / 2);

            foreach (var child in row)
            {
                var size = child.DesiredSize;
                child.Arrange(new Rect(x, y, size.Width, size.Height));
                x += size.Width;
            }

            y += height;
        }

        return finalSize;
    }
}
