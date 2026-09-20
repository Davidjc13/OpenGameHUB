using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OpenGameHUB.ViewModels;
using OpenGameHUB.ViewModels.MainWindow;

namespace OpenGameHUB.Controls;

public partial class GameGridCard : UserControl
{
    public static readonly StyledProperty<double> CoverHeightProperty =
        AvaloniaProperty.Register<GameGridCard, double>(nameof(CoverHeight), 140);

    private MainWindowLibraryViewModel? _library;

    public GameGridCard()
    {
        InitializeComponent();
    }

    public double CoverHeight
    {
        get => GetValue(CoverHeightProperty);
        set => SetValue(CoverHeightProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (GetMainViewModel()?.Library is not { } library)
            return;

        _library = library;
        _library.PropertyChanged += OnLibraryPropertyChanged;
        ApplyLibraryMetrics();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_library is not null)
        {
            _library.PropertyChanged -= OnLibraryPropertyChanged;
            _library = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowLibraryViewModel.GridCardSize)
            or nameof(MainWindowLibraryViewModel.GridCoverHeight)
            or nameof(MainWindowLibraryViewModel.GridCardWidth))
        {
            ApplyLibraryMetrics();
        }
    }

    private void ApplyLibraryMetrics()
    {
        if (_library is null)
            return;

        Width = _library.GridCardSize;
        CoverHeight = _library.GridCoverHeight;
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && GetMainViewModel() is { } vm)
            vm.Library.SelectGameCommand.Execute(game);
    }

    private void OnGameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && GetMainViewModel() is { } vm)
        {
            vm.Library.LaunchGameCommand.Execute(game);
            e.Handled = true;
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && GetMainViewModel() is { } vm)
            vm.Library.LaunchGameCommand.Execute(game);

        e.Handled = true;
    }

    private void OnManageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && sender is Control host)
            GameCardContextMenu.TryShow(host, game);

        e.Handled = true;
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is GameItemViewModel game)
            GameCardContextMenu.Show(this, game, e);
    }

    private MainWindowViewModel? GetMainViewModel()
    {
        return TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;
    }
}
