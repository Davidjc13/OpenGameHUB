using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Domain.Enums;

namespace OpenGameHUB.ViewModels;

public sealed partial class ConsoleCategoryItem : ObservableObject
{
    private readonly Action<ConsoleCategoryItem> _select;

    public ConsoleCategoryItem(
        ConsoleCategoryKind kind,
        Platform? platform,
        string label,
        Action<ConsoleCategoryItem> select)
    {
        Kind = kind;
        Platform = platform;
        Label = label;
        _select = select;
    }

    public ConsoleCategoryKind Kind { get; }

    public Platform? Platform { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => _select(this);
}
