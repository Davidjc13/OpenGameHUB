using Avalonia.Input;

namespace OpenGameHUB.ViewModels;

public enum LibraryHotkey
{
    None,
    FocusSearch,
    Dismiss,
    LaunchSelected,
    RefreshLibrary,
    OpenSettings,
    AddCustomGame,
    ToggleFavorite,
    SetGridView,
    SetListView,
    PreviousPage,
    NextPage
}

public enum LibraryDismiss
{
    None,
    ClearSearch,
    CloseDetail
}

public readonly record struct LibraryHotkeyContext(
    bool SearchFocused,
    bool EditorFocused,
    bool ListFocused,
    bool ButtonFocused,
    bool PopupOpen);

public static class LibraryHotkeys
{
    public static LibraryHotkey Match(
        Key key,
        KeyModifiers modifiers,
        string? symbol,
        LibraryHotkeyContext context)
    {
        if (context.PopupOpen)
            return LibraryHotkey.None;

        if (modifiers == KeyModifiers.Control)
        {
            return key switch
            {
                Key.F => LibraryHotkey.FocusSearch,
                Key.R => LibraryHotkey.RefreshLibrary,
                Key.N => LibraryHotkey.AddCustomGame,
                Key.D => LibraryHotkey.ToggleFavorite,
                Key.D1 => LibraryHotkey.SetGridView,
                Key.D2 => LibraryHotkey.SetListView,
                Key.OemComma => LibraryHotkey.OpenSettings,
                _ => symbol == "," ? LibraryHotkey.OpenSettings : LibraryHotkey.None
            };
        }

        if (modifiers != KeyModifiers.None)
            return LibraryHotkey.None;

        if (key == Key.F5)
            return LibraryHotkey.RefreshLibrary;

        if (key == Key.Escape && !context.EditorFocused)
            return LibraryHotkey.Dismiss;

        if (context.EditorFocused || context.ListFocused)
            return LibraryHotkey.None;

        if (key == Key.Enter && !context.ButtonFocused)
            return LibraryHotkey.LaunchSelected;

        if (context.SearchFocused || context.ButtonFocused)
            return LibraryHotkey.None;

        return key switch
        {
            Key.PageUp => LibraryHotkey.PreviousPage,
            Key.PageDown => LibraryHotkey.NextPage,
            _ => LibraryHotkey.None
        };
    }

    public static LibraryDismiss ResolveDismiss(bool searchFocused, bool searchHasText, bool detailOpen)
    {
        if (searchHasText || searchFocused)
            return LibraryDismiss.ClearSearch;

        if (detailOpen)
            return LibraryDismiss.CloseDetail;

        return LibraryDismiss.None;
    }
}
