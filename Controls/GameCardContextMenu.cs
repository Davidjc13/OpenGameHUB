using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using OpenGameHUB.Services.Games;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Controls;

internal static class GameCardContextMenu
{
    public static void Show(Control host, GameItemViewModel game, ContextRequestedEventArgs e)
    {
        if (TryShow(host, game, PlacementMode.Pointer))
            e.Handled = true;
    }

    public static bool TryShow(
        Control host,
        GameItemViewModel game,
        PlacementMode placement = PlacementMode.Bottom)
    {
        if (TopLevel.GetTopLevel(host)?.DataContext is not MainWindowViewModel vm)
            return false;

        var menu = Create(vm, game);
        if (menu is null)
            return false;

        menu.Placement = placement;
        menu.Open(host);
        return true;
    }

    private static ContextMenu? Create(MainWindowViewModel vm, GameItemViewModel game)
    {
        var menu = new ContextMenu { MaxWidth = 280 };
        var hasItems = false;

        if (vm.Sidebar.HasUserCollections)
        {
            var collections = vm.Sidebar.GetContextMenuCollections(game);
            if (collections.Count > 0)
            {
                var submenu = new MenuItem { Header = vm.Strings.AddToCollection };
                foreach (var item in collections)
                {
                    submenu.Items.Add(new MenuItem
                    {
                        Header = item.Name,
                        ToggleType = MenuItemToggleType.CheckBox,
                        IsChecked = item.IsMember,
                        Command = vm.Sidebar.ToggleGameInCollectionCommand,
                        CommandParameter = new CollectionToggleRequest(game, item.CollectionId)
                    });
                }

                menu.Items.Add(submenu);
                hasItems = true;
            }
        }

        var canOpenFolder = GameLibraryActions.CanOpenInstallFolder(game.Source);
        var canViewStore = GameLibraryActions.CanViewInStore(game.Source);
        var canUninstall = GameLibraryActions.CanUninstall(game.Source);
        var canRemove = GameLibraryActions.CanRemoveFromLibrary(game.Source);
        var hasManagement = canOpenFolder || canViewStore || canUninstall || canRemove;

        if (hasItems && hasManagement)
            menu.Items.Add(new Separator());

        if (canOpenFolder)
        {
            menu.Items.Add(new MenuItem
            {
                Header = vm.Strings.OpenInstallFolder,
                Command = vm.Library.OpenInstallFolderCommand,
                CommandParameter = game
            });
            hasItems = true;
        }

        if (canViewStore)
        {
            menu.Items.Add(new MenuItem
            {
                Header = vm.Strings.ViewInStore,
                Command = vm.Library.OpenStorePageCommand,
                CommandParameter = game
            });
            hasItems = true;
        }

        if (canUninstall)
        {
            menu.Items.Add(new MenuItem
            {
                Header = vm.Strings.UninstallGame,
                Command = vm.Library.UninstallGameCommand,
                CommandParameter = game
            });
            hasItems = true;
        }

        if (canRemove)
        {
            menu.Items.Add(new MenuItem
            {
                Header = vm.Strings.RemoveFromLibrary,
                Command = vm.Library.RemoveCustomGameCommand,
                CommandParameter = game
            });
            hasItems = true;
        }

        return hasItems ? menu : null;
    }
}
