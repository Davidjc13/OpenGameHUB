using Avalonia.Controls;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class LegendaryPromptWindow : Window
{
    public LegendaryPromptWindow()
    {
        InitializeComponent();
    }

    public LegendaryPromptWindow(LegendaryPromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
