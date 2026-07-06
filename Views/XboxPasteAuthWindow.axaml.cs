using Avalonia.Controls;

namespace OpenGameHUB.Views;

public partial class XboxPasteAuthWindow : Window
{
    public XboxPasteAuthWindow()
    {
        InitializeComponent();
    }

    public XboxPasteAuthWindow(ViewModels.XboxPasteAuthViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
