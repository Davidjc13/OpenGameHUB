using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        QueueViewportUpdate();
    }

    private void OnGamesViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not ScrollViewer scrollViewer)
            return;

        vm.UpdateLibraryViewport(scrollViewer.Bounds.Width, scrollViewer.Bounds.Height);
    }

    private void QueueViewportUpdate()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.UpdateLibraryViewport(GamesScrollViewer.Bounds.Width, GamesScrollViewer.Bounds.Height);
        }, DispatcherPriority.Loaded);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not MainWindowViewModel viewModel)
            return;

        var text = textBox.Text ?? string.Empty;
        if (viewModel.SearchText != text)
            viewModel.SearchText = text;
    }

    private async void OnInstallAppUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.Updates.InstallUpdateCommand.ExecuteAsync(null);
        e.Handled = true;
    }
}
