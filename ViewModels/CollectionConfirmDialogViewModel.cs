using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;

namespace OpenGameHUB.ViewModels;

public partial class CollectionConfirmDialogViewModel : ViewModelBase
{
    public CollectionConfirmDialogViewModel(string title, string message, string? confirmLabel = null)
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel ?? Loc.T("DeleteCollection");
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmLabel { get; }

    public bool Confirmed { get; private set; }

    public LocalizedStrings Strings { get; } = new();

    [RelayCommand]
    private void Confirm(Window window)
    {
        Confirmed = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window) => window.Close();
}
