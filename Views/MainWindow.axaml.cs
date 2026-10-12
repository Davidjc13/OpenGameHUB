using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenGameHUB.Controls;
using OpenGameHUB.ViewModels;
using OpenGameHUB.ViewModels.MainWindow;

namespace OpenGameHUB.Views;

public partial class MainWindow : Window
{
    private readonly Transitions _pageSlideTransitions = new()
    {
        new DoubleTransition
        {
            Property = TranslateTransform.YProperty,
            Duration = TimeSpan.FromMilliseconds(380),
            Easing = new CubicEaseOut()
        }
    };

    private MainWindowLibraryViewModel? _library;
    private TranslateTransform? _pageSlideTransform;
    private int _pageSlideRun;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _library = vm.Library;
            _library.PropertyChanged += OnLibraryPropertyChanged;
        }

        QueueViewportUpdate();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_library is not null)
            _library.PropertyChanged -= OnLibraryPropertyChanged;

        base.OnClosed(e);
    }

    private void OnLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowLibraryViewModel.PageSlideGeneration) || _library is null)
            return;

        var direction = _library.PageSlideDirection;
        if (direction == 0)
            return;

        // The page swap happens after this notification returns. Slide the new page in on the next pass.
        var captured = direction;
        Dispatcher.UIThread.Post(() => SlidePageIn(captured), DispatcherPriority.Loaded);
    }

    private void SlidePageIn(int direction)
    {
        var transform = _pageSlideTransform ??= new TranslateTransform();
        GamesSlideHost.RenderTransform = transform;

        var distance = GamesScrollViewer.Bounds.Height;
        if (distance < 1)
            distance = GamesSlideHost.Bounds.Height;
        if (distance < 1)
            return;

        var from = direction > 0 ? distance : -distance;
        var run = ++_pageSlideRun;

        transform.Transitions = null;
        transform.Y = from;

        Dispatcher.UIThread.Post(() =>
        {
            if (run != _pageSlideRun)
                return;

            transform.Transitions = _pageSlideTransitions;
            transform.Y = 0;
            ScheduleSlideReset(transform, run);
        }, DispatcherPriority.Render);
    }

    private void ScheduleSlideReset(TranslateTransform transform, int run)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(450);
            if (run != _pageSlideRun || Math.Abs(transform.Y) < 0.5)
                return;

            transform.Transitions = null;
            transform.Y = 0;
        }, DispatcherPriority.Background);
    }

    private void OnGamesViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not ScrollViewer scrollViewer)
            return;

        vm.Library.UpdateLibraryViewport(scrollViewer.Bounds.Width, scrollViewer.Bounds.Height);
    }

    private void QueueViewportUpdate()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.Library.UpdateLibraryViewport(GamesScrollViewer.Bounds.Width, GamesScrollViewer.Bounds.Height);
        }, DispatcherPriority.Loaded);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not MainWindowViewModel viewModel)
            return;

        var text = textBox.Text ?? string.Empty;
        if (viewModel.Sidebar.SearchText != text)
            viewModel.Sidebar.SearchText = text;
    }

    private void OnCollectionMembershipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox
            || checkBox.DataContext is not CollectionMembershipItem item
            || DataContext is not MainWindowViewModel vm
            || vm.Library.SelectedGame is null)
            return;

        vm.Sidebar.ToggleGameInCollection(vm.Library.SelectedGame, item.CollectionId);
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

        vm.Sidebar.SelectedLibraryCollection = collectionItem;

        var menu = new ContextMenu { MaxWidth = 200 };
        menu.Items.Add(new MenuItem
        {
            Header = vm.Strings.RenameCollection,
            Command = vm.Sidebar.RenameCollectionCommand
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = vm.Strings.DeleteCollection,
            Command = vm.Sidebar.DeleteCollectionCommand
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

    private void OnManageGameClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm
            || vm.Library.SelectedGame is null
            || sender is not Control host)
            return;

        GameCardContextMenu.TryShow(host, vm.Library.SelectedGame);
        e.Handled = true;
    }

    private async void OnInstallAppUpdateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        await vm.Updates.InstallUpdateCommand.ExecuteAsync(null);
        e.Handled = true;
    }
}
