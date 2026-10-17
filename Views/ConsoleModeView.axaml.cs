using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class ConsoleModeView : UserControl
{
    private const double ReferenceViewportHeight = 1080;

    private ConsoleModeViewModel? _console;
    private MainWindowViewModel? _window;
    private bool _syncingCarousel;

    public ConsoleModeView()
    {
        InitializeComponent();
        SearchBox.GotFocus += OnSearchGotFocus;
        SearchBox.LostFocus += OnSearchLostFocus;
        ExitButton.GotFocus += (_, _) => SetMenu(ConsoleMenuRegion.Exit);
        PlayButton.GotFocus += (_, _) => SetMenu(ConsoleMenuRegion.Play);
        CategoryBar.GotFocus += (_, _) => SetMenu(ConsoleMenuRegion.Categories);
        Carousel.GotFocus += (_, _) => SetMenu(ConsoleMenuRegion.Games);
        MediaDock.GotFocus += (_, _) => SetMenu(ConsoleMenuRegion.Media);
    }

    public void FocusCarousel()
    {
        Dispatcher.UIThread.Post(ScrollToFocusedGame, DispatcherPriority.Input);
    }

    public void FocusSearch(bool showKeyboard = false)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
        if (showKeyboard && _console is not null && !_console.KeyboardOpen)
            _console.OpenSearchKeyboard();
        else if (_console is not null)
            _console.MenuRegion = ConsoleMenuRegion.Search;
    }

    public bool IsConsoleSearch(IInputElement? element) => ReferenceEquals(element, SearchBox);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_console is not null)
        {
            _console.PropertyChanged -= OnConsolePropertyChanged;
            _console.FocusRequested -= OnFocusRequested;
            _console.SearchRequested -= OnSearchRequested;
        }

        _console = DataContext as ConsoleModeViewModel;
        if (_console is not null)
        {
            _console.PropertyChanged += OnConsolePropertyChanged;
            _console.FocusRequested += OnFocusRequested;
            _console.SearchRequested += OnSearchRequested;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_window is not null)
            _window.PropertyChanged -= OnWindowPropertyChanged;

        _window = this.FindAncestorOfType<Window>()?.DataContext as MainWindowViewModel;
        if (_window is not null)
            _window.PropertyChanged += OnWindowPropertyChanged;

        UpdateStatus();
        ApplyViewportLayout(Bounds.Size);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyViewportLayout(e.NewSize);
    }

    private void ApplyViewportLayout(Size viewportSize)
    {
        if (viewportSize.Height <= 0)
            return;

        var scale = Math.Clamp(viewportSize.Height / ReferenceViewportHeight, 0.72, 1.0);
        Classes.Set("console-compact", scale < 0.88);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
            _window.PropertyChanged -= OnWindowPropertyChanged;

        _window = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSearchGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConsoleModeViewModel vm)
        {
            vm.SearchFocused = true;
            if (vm.MenuRegion != ConsoleMenuRegion.Search && !vm.KeyboardOpen)
                vm.MenuRegion = ConsoleMenuRegion.Search;
        }
    }

    private void OnSearchLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConsoleModeViewModel vm && !vm.KeyboardOpen && vm.MenuRegion == ConsoleMenuRegion.Search)
            vm.SearchFocused = false;
    }

    private void OnConsolePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConsoleModeViewModel.FocusedIndex))
            ScrollToFocusedGame(focusCarousel: false);
        else if (e.PropertyName == nameof(ConsoleModeViewModel.MenuRegion))
            ApplyMenuFocus();
    }

    private void SetMenu(ConsoleMenuRegion region)
    {
        if (_console is not null && _console.MenuRegion != region && !_console.KeyboardOpen)
            _console.MenuRegion = region;
    }

    private void ApplyMenuFocus()
    {
        if (_console is null || _console.KeyboardOpen)
            return;

        switch (_console.MenuRegion)
        {
            case ConsoleMenuRegion.Search:
                SearchBox.Focus();
                SearchBox.SelectAll();
                break;
            case ConsoleMenuRegion.Exit:
                ExitButton.Focus();
                break;
            case ConsoleMenuRegion.Play:
                PlayButton.Focus();
                break;
            case ConsoleMenuRegion.Categories:
                CategoryBar.Focus();
                break;
            case ConsoleMenuRegion.Media:
                MediaDock.Focus();
                break;
            default:
                ScrollToFocusedGame(focusCarousel: true);
                break;
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        ScrollToFocusedGame(focusCarousel: true);

    private void OnSearchRequested(object? sender, EventArgs e)
    {
        if (_console?.KeyboardOpen == true)
            SearchBox.Focus();
        else
            Dispatcher.UIThread.Post(FocusCarousel, DispatcherPriority.Input);
    }

    private void OnWindowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.StatusText))
            UpdateStatus();
    }

    private void UpdateStatus()
    {
        var text = _window?.StatusText ?? string.Empty;
        StatusLine.Text = text;
        StatusLine.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    private void ScrollToFocusedGame() => ScrollToFocusedGame(focusCarousel: true);

    private void ScrollToFocusedGame(bool focusCarousel)
    {
        if (DataContext is not ConsoleModeViewModel vm)
            return;

        _syncingCarousel = true;
        Carousel.SelectedIndex = vm.FocusedIndex;
        _syncingCarousel = false;

        if (vm.FocusedIndex >= 0 && vm.FocusedIndex < vm.Games.Count)
            Carousel.ScrollIntoView(vm.Games[vm.FocusedIndex]);

        if (focusCarousel)
            Carousel.Focus();
    }

    private void OnCarouselSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingCarousel || DataContext is not ConsoleModeViewModel vm)
            return;

        if (Carousel.SelectedIndex < 0 || Carousel.SelectedIndex == vm.FocusedIndex)
            return;

        vm.FocusedIndex = Carousel.SelectedIndex;
    }

    private void OnCarouselDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ConsoleModeViewModel vm)
            return;

        vm.PlayFocusedCommand.Execute(null);
        e.Handled = true;
    }
}
