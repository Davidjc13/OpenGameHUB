using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    private void OnCollectionMembershipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox
            || checkBox.DataContext is not CollectionMembershipItem item
            || DataContext is not MainWindowViewModel vm
            || vm.SelectedGame is null)
            return;

        vm.ToggleGameInCollection(vm.SelectedGame, item.CollectionId);
        e.Handled = true;
    }

    private void OnLibraryCollectionContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not ListBox listBox)
            return;

        if (e.Source is not Control source)
            return;

        var collectionItem = FindAncestor<ListBoxItem>(source)?.DataContext as LibraryCollectionItem
            ?? listBox.SelectedItem as LibraryCollectionItem;

        if (collectionItem is null || !collectionItem.IsUserCollection)
            return;

        vm.SelectedLibraryCollection = collectionItem;

        var menu = new ContextMenu { MaxWidth = 200 };
        menu.Items.Add(new MenuItem
        {
            Header = vm.Strings.RenameCollection,
            Command = vm.RenameCollectionCommand
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = vm.Strings.DeleteCollection,
            Command = vm.DeleteCollectionCommand
        });

        menu.Open(listBox);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(Control control) where T : Control
    {
        var current = control as Visual;
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = current.GetVisualParent();
        }

        return null;
    }

    private async void OnInstallAppUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.Updates.InstallUpdateCommand.ExecuteAsync(null);
        e.Handled = true;
    }
}
