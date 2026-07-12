using Avalonia.Controls;
using Avalonia.Input;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Controls;

internal static class GameCardContextMenu
{
    public static void Show(Control host, GameItemViewModel game, ContextRequestedEventArgs e)
    {
        if (TopLevel.GetTopLevel(host)?.DataContext is not MainWindowViewModel vm || !vm.HasUserCollections)
            return;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = vm.Strings.AddToCollection,
            IsEnabled = false
        });

        foreach (var item in vm.GetContextMenuCollections(game))
        {
            menu.Items.Add(new MenuItem
            {
                Header = item.IsMember
                    ? $"- {item.Name}"
                    : item.Name,
                Command = vm.ToggleGameInCollectionCommand,
                CommandParameter = new CollectionToggleRequest(game, item.CollectionId)
            });
        }

        menu.Open(host);
        e.Handled = true;
    }
}
