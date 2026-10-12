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
using OpenGameHUB.Infrastructure.Input;
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
    private MainWindowViewModel? _windowVm;
    private TranslateTransform? _pageSlideTransform;
    private int _pageSlideRun;
    private bool _consoleChromeActive;
    private bool _restoreBoundsValid;
    private WindowState _restoreWindowState;
    private WindowDecorations _restoreDecorations;
    private bool _restoreCanResize;
    private PixelPoint _restorePosition;
    private double _restoreWidth;
    private double _restoreHeight;
    private readonly XInputGamepadPoller _gamepad = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _windowVm = vm;
            ConsoleView.DataContext = vm.Console;
            ConsoleView.IsVisible = false;
            _library = vm.Library;
            _library.PropertyChanged += OnLibraryPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        AddHandler(KeyDownEvent, OnConsoleKeyDown, RoutingStrategies.Tunnel);
        _gamepad.Input += OnGamepadInput;
        _gamepad.Start();
        QueueViewportUpdate();
    }

    protected override void OnClosed(EventArgs e)
    {
        RemoveHandler(KeyDownEvent, OnConsoleKeyDown);
        _gamepad.Input -= OnGamepadInput;
        _gamepad.Dispose();

        if (_library is not null)
            _library.PropertyChanged -= OnLibraryPropertyChanged;

        if (_windowVm is not null)
            _windowVm.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsConsoleMode) && _windowVm is not null)
            ApplyConsoleChrome(_windowVm.IsConsoleMode);
    }

    private void ApplyConsoleChrome(bool enabled)
    {
        if (enabled)
        {
            ConsoleView.IsVisible = true;
            EnterConsoleChrome();
            Dispatcher.UIThread.Post(() => ConsoleView.FocusCarousel(), DispatcherPriority.Input);
            return;
        }

        ConsoleView.IsVisible = false;
        ExitConsoleChrome();
    }

    private void EnterConsoleChrome()
    {
        if (_consoleChromeActive)
            return;

        _consoleChromeActive = true;
        _restoreWindowState = WindowState;
        _restoreDecorations = WindowDecorations;
        _restoreCanResize = CanResize;
        _restoreBoundsValid = WindowState == WindowState.Normal;
        if (_restoreBoundsValid)
        {
            _restorePosition = Position;
            _restoreWidth = Width;
            _restoreHeight = Height;
        }

        if (WindowState != WindowState.Normal)
            WindowState = WindowState.Normal;

        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CanResize = false;
        WindowState = WindowState.FullScreen;
    }

    private void ExitConsoleChrome()
    {
        if (!_consoleChromeActive)
            return;

        _consoleChromeActive = false;
        WindowState = WindowState.Normal;
        WindowDecorations = _restoreDecorations;
        CanResize = _restoreCanResize;
        if (_restoreBoundsValid)
        {
            Width = _restoreWidth;
            Height = _restoreHeight;
            Position = _restorePosition;
        }

        WindowState = _restoreWindowState;
    }

    private void OnGamepadInput(ConsoleInput input)
    {
        if (_windowVm is null)
            return;

        if (_windowVm.IsConsoleMode)
            _windowVm.Console.ApplyInput(input);
        else
            _windowVm.ApplyWindowGamepad(input);
    }

    private void OnConsoleKeyDown(object? sender, KeyEventArgs e)
    {
        if (_windowVm is null)
            return;

        if (e.Key == Key.F11)
        {
            _windowVm.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (!_windowVm.IsConsoleMode)
        {
            if (FocusOwnsArrows(FocusManager?.GetFocusedElement()))
                return;

            var windowInput = e.Key switch
            {
                Key.Left => ConsoleInput.MoveLeft,
                Key.Right => ConsoleInput.MoveRight,
                Key.Up => ConsoleInput.MoveUp,
                Key.Down => ConsoleInput.MoveDown,
                _ => (ConsoleInput?)null
            };

            if (windowInput is null)
                return;

            _windowVm.ApplyWindowGamepad(windowInput.Value);
            e.Handled = true;
            return;
        }

        var focused = FocusManager?.GetFocusedElement();
        var searchFocused = ConsoleView.IsConsoleSearch(focused);
        if (searchFocused != _windowVm.Console.SearchFocused)
            _windowVm.Console.SearchFocused = searchFocused;

        if (e.Key == Key.Escape)
        {
            _windowVm.Console.ApplyInput(ConsoleInput.Back);
            e.Handled = true;
            return;
        }

        if (searchFocused)
            return;

        if (focused is Button && e.Key is Key.Enter or Key.Space)
            return;

        var input = e.Key switch
        {
            Key.Left => ConsoleInput.MoveLeft,
            Key.Right => ConsoleInput.MoveRight,
            Key.Q => ConsoleInput.PreviousCategory,
            Key.E => ConsoleInput.NextCategory,
            Key.Enter or Key.A => ConsoleInput.Activate,
            Key.Y => ConsoleInput.Favorite,
            _ => (ConsoleInput?)null
        };

        if (input is null)
            return;

        _windowVm.Console.ApplyInput(input.Value, textConsumesHorizontal: searchFocused);
        e.Handled = true;
    }

    private static bool FocusOwnsArrows(object? focused)
    {
        if (focused is not Visual visual)
            return false;

        return visual is TextBox or ComboBox
            || visual.FindAncestorOfType<TextBox>() is not null
            || visual.FindAncestorOfType<ComboBox>() is not null
            || visual.FindAncestorOfType<ListBox>() is not null;
    }

    private void OnLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowLibraryViewModel.SelectedGame))
        {
            Dispatcher.UIThread.Post(BringSelectedGameIntoView, DispatcherPriority.Render);
            return;
        }

        if (e.PropertyName != nameof(MainWindowLibraryViewModel.PageSlideGeneration) || _library is null)
            return;

        var direction = _library.PageSlideDirection;
        if (direction == 0)
            return;

        // The page swap happens after this notification returns. Slide the new page in on the next pass.
        var captured = direction;
        Dispatcher.UIThread.Post(() => SlidePageIn(captured), DispatcherPriority.Loaded);
    }

    private void BringSelectedGameIntoView()
    {
        if (TryBringSelectedGameIntoView())
            return;

        Dispatcher.UIThread.Post(() => TryBringSelectedGameIntoView(), DispatcherPriority.Background);
    }

    private bool TryBringSelectedGameIntoView()
    {
        if (_library?.SelectedGame is not { } selected)
            return true;

        foreach (var visual in GamesSlideHost.GetVisualDescendants())
        {
            if (visual is Control control && ReferenceEquals(control.DataContext, selected))
            {
                control.BringIntoView();
                return true;
            }
        }

        return false;
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
