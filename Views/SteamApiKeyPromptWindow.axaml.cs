using Avalonia.Controls;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class SteamApiKeyPromptWindow : Window
{
    public SteamApiKeyPromptWindow()
    {
        InitializeComponent();
    }

    public SteamApiKeyPromptWindow(SteamApiKeyPromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
