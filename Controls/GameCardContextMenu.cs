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

        var collections = vm.GetContextMenuCollections(game);
        if (collections.Count == 0)
            return;

        var menu = new ContextMenu { MaxWidth = 240 };
        var submenu = new MenuItem { Header = vm.Strings.AddToCollection };

        foreach (var item in collections)
        {
            submenu.Items.Add(new MenuItem
            {
                Header = item.Name,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = item.IsMember,
                Command = vm.ToggleGameInCollectionCommand,
                CommandParameter = new CollectionToggleRequest(game, item.CollectionId)
            });
        }

        menu.Items.Add(submenu);
        menu.Open(host);
        e.Handled = true;
    }
}
