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
    private bool _applyingChrome;
    private bool _restoreBoundsValid;
    private WindowState _restoreWindowState;
    private WindowDecorations _restoreDecorations;
    private bool _restoreCanResize;
    private PixelPoint _restorePosition;
    private double _restoreWidth;
    private double _restoreHeight;
    private readonly XInputGamepadPoller _gamepad = new();
    private readonly GamepadRepeatState _windowGamepadRepeat = new();
    private readonly HashSet<Key> _heldKeys = [];
    private int _modalHotkeyDepth;

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
            _library.GameLaunched += OnGameLaunched;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        AddHandler(KeyDownEvent, OnConsoleKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnLibraryKeyUp, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnLibraryTextInput, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerUsed, RoutingStrategies.Tunnel);
        _gamepad.Frame += OnGamepadFrame;
        _gamepad.Start();
        QueueViewportUpdate();
    }

    protected override void OnClosed(EventArgs e)
    {
        RemoveHandler(KeyDownEvent, OnConsoleKeyDown);
        RemoveHandler(KeyUpEvent, OnLibraryKeyUp);
        RemoveHandler(TextInputEvent, OnLibraryTextInput);
        RemoveHandler(PointerPressedEvent, OnPointerUsed);
        _gamepad.Frame -= OnGamepadFrame;
        _gamepad.Dispose();

        if (_library is not null)
        {
            _library.PropertyChanged -= OnLibraryPropertyChanged;
            _library.GameLaunched -= OnGameLaunched;
        }

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
        _windowGamepadRepeat.Reset();
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

        _applyingChrome = true;
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
        _applyingChrome = false;
    }

    private void ExitConsoleChrome()
    {
        if (!_consoleChromeActive)
            return;

        _applyingChrome = true;
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
        _applyingChrome = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != WindowStateProperty || _applyingChrome || !_consoleChromeActive)
            return;

        if (WindowState is WindowState.FullScreen or WindowState.Minimized)
            return;

        _applyingChrome = true;
        WindowState = WindowState.FullScreen;
        _applyingChrome = false;
        _windowVm?.Console.ResumePresence();
    }

    private void OnGameLaunched(object? sender, EventArgs e)
    {
        if (!_consoleChromeActive || _windowVm is null)
            return;

        _windowVm.Console.SuspendPresence();
        _applyingChrome = true;
        WindowState = WindowState.Minimized;
        _applyingChrome = false;
    }

    private void OnPointerUsed(object? sender, PointerPressedEventArgs e) =>
        _windowVm?.Console.NoteKeyboardOrMouse();

    private void OnGamepadFrame(GamepadButtons previous, GamepadButtons current, TimeSpan elapsed)
    {
        if (_windowVm is null)
            return;

        if (GamepadInput.HasActivity(previous, current))
            _windowVm.Console.NoteGamepad();

        if (_windowVm.IsConsoleMode)
        {
            _windowVm.Console.ProcessGamepad(previous, current, elapsed);
            return;
        }

        foreach (var input in GamepadInput.Collect(previous, current, _windowGamepadRepeat, elapsed))
            _windowVm.ApplyWindowGamepad(input);
    }

    private void OnConsoleKeyDown(object? sender, KeyEventArgs e)
    {
        if (_windowVm is null || e.Handled)
            return;

        _windowVm.Console.NoteKeyboardOrMouse();

        if (e.Key == Key.F11 && e.KeyModifiers == KeyModifiers.None)
        {
            _windowVm.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (_windowVm.IsConsoleMode)
        {
            HandleConsoleKey(e);
            return;
        }

        HandleLibraryKey(e);
    }

    private void OnLibraryKeyUp(object? sender, KeyEventArgs e) => _heldKeys.Remove(e.Key);

    private void HandleConsoleKey(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.F:
                    ConsoleView.FocusSearch();
                    e.Handled = true;
                    return;
                case Key.R:
                    RefreshFromHotkey();
                    e.Handled = true;
                    return;
                case Key.N:
                    AddGameFromHotkey();
                    e.Handled = true;
                    return;
                case Key.D:
                    _windowVm!.Console.ApplyInput(ConsoleInput.Favorite);
                    e.Handled = true;
                    return;
                case Key.OemComma when e.KeySymbol is null || e.KeySymbol == ",":
                    OpenSettingsFromHotkey();
                    e.Handled = true;
                    return;
            }

            if (e.KeySymbol == ",")
            {
                OpenSettingsFromHotkey();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.F5 && e.KeyModifiers == KeyModifiers.None)
        {
            RefreshFromHotkey();
            e.Handled = true;
            return;
        }

        var focused = FocusManager?.GetFocusedElement();
        var searchFocused = ConsoleView.IsConsoleSearch(focused);
        if (searchFocused != _windowVm!.Console.SearchFocused)
            _windowVm.Console.SearchFocused = searchFocused;

        if (e.Key == Key.Escape)
        {
            _windowVm.Console.ApplyInput(ConsoleInput.Back);
            e.Handled = true;
            return;
        }

        if (searchFocused)
            return;

        if (e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift
            && e.Key is Key.Add or Key.OemPlus)
        {
            _windowVm.Console.Media.BumpVolume(SystemMediaViewModel.VolumeStep);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Subtract or Key.OemMinus)
        {
            _windowVm.Console.Media.BumpVolume(-SystemMediaViewModel.VolumeStep);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.V)
        {
            _windowVm.Console.Media.ToggleMute();
            e.Handled = true;
            return;
        }

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
            Key.M => ConsoleInput.MediaToggle,
            _ => (ConsoleInput?)null
        };

        if (input is null)
            return;

        _windowVm.Console.ApplyInput(input.Value, textConsumesHorizontal: searchFocused);
        e.Handled = true;
    }

    private void HandleLibraryKey(KeyEventArgs e)
    {
        var context = DescribeFocus();
        var hotkey = LibraryHotkeys.Match(e.Key, e.KeyModifiers, e.KeySymbol, context);
        if (hotkey != LibraryHotkey.None)
        {
            var repeats = hotkey is LibraryHotkey.PreviousPage or LibraryHotkey.NextPage;
            if (!repeats && _heldKeys.Contains(e.Key))
            {
                e.Handled = true;
                return;
            }

            if (ApplyLibraryHotkey(hotkey))
            {
                if (!repeats)
                    _heldKeys.Add(e.Key);

                e.Handled = true;
            }

            return;
        }

        if (context.PopupOpen || context.EditorFocused || context.ListFocused || context.SearchFocused)
            return;

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

        _windowVm!.ApplyWindowGamepad(windowInput.Value);
        e.Handled = true;
    }

    private void OnLibraryTextInput(object? sender, TextInputEventArgs e)
    {
        if (_windowVm is null || _windowVm.IsConsoleMode || string.IsNullOrEmpty(e.Text) || e.Text.All(char.IsControl))
            return;

        var context = DescribeFocus();
        if (context.PopupOpen || context.EditorFocused || context.ListFocused || context.SearchFocused)
            return;

        if (string.IsNullOrEmpty(_windowVm.Sidebar.SearchText) && string.IsNullOrWhiteSpace(e.Text))
            return;

        FocusLibrarySearch(e.Text);
        e.Handled = true;
    }

    private bool ApplyLibraryHotkey(LibraryHotkey hotkey)
    {
        if (_windowVm is null)
            return false;

        switch (hotkey)
        {
            case LibraryHotkey.FocusSearch:
                FocusLibrarySearch();
                return true;
            case LibraryHotkey.Dismiss:
                return DismissLibrary();
            case LibraryHotkey.LaunchSelected:
                LaunchFromHotkey();
                return true;
            case LibraryHotkey.RefreshLibrary:
                RefreshFromHotkey();
                return true;
            case LibraryHotkey.OpenSettings:
                OpenSettingsFromHotkey();
                return true;
            case LibraryHotkey.AddCustomGame:
                AddGameFromHotkey();
                return true;
            case LibraryHotkey.ToggleFavorite:
                _windowVm.Library.ToggleFavoriteCommand.Execute(_windowVm.Library.SelectedGame);
                return true;
            case LibraryHotkey.SetGridView:
                _windowVm.Library.SetGridViewCommand.Execute(null);
                return true;
            case LibraryHotkey.SetListView:
                _windowVm.Library.SetListViewCommand.Execute(null);
                return true;
            case LibraryHotkey.PreviousPage:
                if (!_windowVm.Library.PreviousPageCommand.CanExecute(null))
                    return false;

                _windowVm.Library.PreviousPageCommand.Execute(null);
                return true;
            case LibraryHotkey.NextPage:
                if (!_windowVm.Library.NextPageCommand.CanExecute(null))
                    return false;

                _windowVm.Library.NextPageCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private void FocusLibrarySearch(string? append = null)
    {
        SearchBox.Focus();
        if (append is null)
        {
            SearchBox.SelectAll();
            return;
        }

        var text = (SearchBox.Text ?? string.Empty) + append;
        SearchBox.Text = text;
        SearchBox.CaretIndex = text.Length;
    }

    private bool DismissLibrary()
    {
        if (_windowVm is null)
            return false;

        var dismiss = LibraryHotkeys.ResolveDismiss(
            ReferenceEquals(FocusManager?.GetFocusedElement(), SearchBox),
            !string.IsNullOrEmpty(_windowVm.Sidebar.SearchText),
            _windowVm.Library.HasSelectedGame);
        switch (dismiss)
        {
            case LibraryDismiss.ClearSearch:
                SearchBox.Text = string.Empty;
                _windowVm.Sidebar.SearchText = string.Empty;
                GamesScrollViewer.Focus();
                return true;
            case LibraryDismiss.CloseDetail:
                _windowVm.Library.CloseDetailCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }

    private void LaunchFromHotkey()
    {
        if (_windowVm is null)
            return;

        if (_windowVm.Library.SelectedGame is null && _windowVm.Library.Games.Count > 0)
            _windowVm.Library.SelectedGame = _windowVm.Library.Games[0];

        if (_windowVm.Library.LaunchSelectedGameCommand.CanExecute(null))
            _windowVm.Library.LaunchSelectedGameCommand.Execute(null);
    }

    private void RefreshFromHotkey()
    {
        if (_windowVm is null || _windowVm.Library.IsRefreshing)
            return;

        _windowVm.RefreshLibraryCommand.Execute(null);
    }

    private void OpenSettingsFromHotkey()
    {
        if (_windowVm is null)
            return;

        RunModalHotkey(() => _windowVm.OpenSettingsCommand.ExecuteAsync(null));
    }

    private void AddGameFromHotkey()
    {
        if (_windowVm is null)
            return;

        RunModalHotkey(() => _windowVm.Sidebar.AddCustomGameCommand.ExecuteAsync(null));
    }

    private void RunModalHotkey(Func<Task> start)
    {
        if (_modalHotkeyDepth > 0)
            return;

        _modalHotkeyDepth++;
        _ = FinishModalHotkey(start);
    }

    private async Task FinishModalHotkey(Func<Task> start)
    {
        try
        {
            await start();
        }
        finally
        {
            _modalHotkeyDepth--;
        }
    }

    private LibraryHotkeyContext DescribeFocus()
    {
        var focused = FocusManager?.GetFocusedElement();
        if (focused is not Visual visual)
            return default;

        var popup = visual is Popup || visual.FindAncestorOfType<Popup>() is not null;
        var search = ReferenceEquals(focused, SearchBox);
        var editor = !search && (visual is TextBox or ComboBox or Slider
            || visual.FindAncestorOfType<TextBox>() is not null
            || visual.FindAncestorOfType<ComboBox>() is not null
            || visual.FindAncestorOfType<Slider>() is not null);
        var list = visual is ListBox || visual.FindAncestorOfType<ListBox>() is not null;
        var button = !search && (visual is Button || visual.FindAncestorOfType<Button>() is not null);
        return new LibraryHotkeyContext(search, editor, list, button, popup);
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
