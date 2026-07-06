using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not MainWindowViewModel viewModel)
            return;

        var text = textBox.Text ?? string.Empty;
        if (viewModel.SearchText != text)
            viewModel.SearchText = text;
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: GameItemViewModel game })
            return;

        if (DataContext is MainWindowViewModel vm)
            vm.SelectGameCommand.Execute(game);
    }

    private void OnGameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: GameItemViewModel game })
            return;

        if (DataContext is MainWindowViewModel vm)
            vm.LaunchGameCommand.Execute(game);

        e.Handled = true;
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: GameItemViewModel game })
            return;

        if (DataContext is MainWindowViewModel vm)
            vm.LaunchGameCommand.Execute(game);

        e.Handled = true;
    }
}
