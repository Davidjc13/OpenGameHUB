using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.ViewModels.MainWindow;

public sealed class SortOptionItem
{
    public SortOptionItem(string label, SortOption option)
    {
        Label = label;
        Option = option;
    }

    public string Label { get; }
    public SortOption Option { get; }
}
