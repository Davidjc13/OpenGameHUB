using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Controls;

public partial class GameListCard : UserControl
{
    public GameListCard()
    {
        InitializeComponent();
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && GetMainViewModel() is { } vm)
            vm.SelectGameCommand.Execute(game);
    }

    private void OnGameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && GetMainViewModel() is { } vm)
        {
            vm.LaunchGameCommand.Execute(game);
            e.Handled = true;
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameItemViewModel game && GetMainViewModel() is { } vm)
            vm.LaunchGameCommand.Execute(game);

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
