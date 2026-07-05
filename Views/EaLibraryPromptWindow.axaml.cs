using Avalonia.Controls;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class EaLibraryPromptWindow : Window
{
    public EaLibraryPromptWindow()
    {
        InitializeComponent();
    }

    public EaLibraryPromptWindow(EaLibraryPromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
