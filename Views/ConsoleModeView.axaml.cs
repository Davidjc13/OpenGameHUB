using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenGameHUB.Infrastructure.Input;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class ConsoleModeView : UserControl
{
    private ConsoleModeViewModel? _console;
    private MainWindowViewModel? _window;
    private bool _syncingCarousel;

    public ConsoleModeView()
    {
        InitializeComponent();
        SearchBox.GotFocus += OnSearchGotFocus;
        SearchBox.LostFocus += OnSearchLostFocus;
    }

    public void FocusCarousel()
    {
        Dispatcher.UIThread.Post(ScrollToFocusedGame, DispatcherPriority.Input);
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
        if (_console?.UsingGamepad == true)
            Dispatcher.UIThread.Post(static () => WindowsOnScreenKeyboard.TryShow(), DispatcherPriority.Input);
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
            vm.SearchFocused = true;
    }

    private void OnSearchLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ConsoleModeViewModel vm)
            vm.SearchFocused = false;
    }

    private void OnConsolePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConsoleModeViewModel.FocusedIndex))
            ScrollToFocusedGame(focusCarousel: false);
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        ScrollToFocusedGame(focusCarousel: true);

    private void OnSearchRequested(object? sender, EventArgs e) => FocusSearch();

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
