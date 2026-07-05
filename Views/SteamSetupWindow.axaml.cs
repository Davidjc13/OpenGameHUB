using Avalonia.Controls;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class SteamSetupWindow : Window
{
    public SteamSetupWindow()
    {
        InitializeComponent();
    }

    public SteamSetupWindow(SteamSetupViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
