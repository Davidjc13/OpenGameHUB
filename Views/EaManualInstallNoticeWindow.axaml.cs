using Avalonia.Controls;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class EaManualInstallNoticeWindow : Window
{
    public EaManualInstallNoticeWindow()
    {
        InitializeComponent();
    }

    public EaManualInstallNoticeWindow(EaManualInstallNoticeViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
