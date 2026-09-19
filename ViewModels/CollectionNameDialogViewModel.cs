using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenGameHUB.Localization;

namespace OpenGameHUB.ViewModels;

public partial class CollectionNameDialogViewModel : ViewModelBase
{
    public CollectionNameDialogViewModel(string title, string prompt, string initialName = "")
    {
        Title = title;
        Prompt = prompt;
        Name = initialName;
    }

    public string Title { get; }
    public string Prompt { get; }

    [ObservableProperty]
    private string _name;

    public bool Confirmed { get; private set; }

    public LocalizedStrings Strings { get; } = new();

    [RelayCommand]
    private void Confirm(Window window)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return;

        Confirmed = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancel(Window window) => window.Close();
}
