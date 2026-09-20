using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.ViewModels.MainWindow;

public sealed class PlatformFilterItem
{
    public PlatformFilterItem(string label, Platform? platform, bool isSelected)
    {
        Label = label;
        Platform = platform;
        IsSelected = isSelected;
    }

    public string Label { get; }
    public Platform? Platform { get; }
    public bool IsSelected { get; }
}
