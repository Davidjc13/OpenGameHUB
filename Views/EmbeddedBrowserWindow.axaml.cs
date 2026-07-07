using Avalonia.Controls;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

internal partial class EmbeddedBrowserWindow : Window
{
    public EmbeddedBrowserWindow()
    {
        InitializeComponent();
    }

    public EmbeddedBrowserWindow(EmbeddedBrowserViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is EmbeddedBrowserViewModel viewModel)
            viewModel.AttachHost(BrowserHost);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is EmbeddedBrowserViewModel viewModel)
            viewModel.DetachHost();

        BrowserHost.Dispose();
    }
}
