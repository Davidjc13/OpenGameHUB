using Avalonia.Input;
using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class LibraryHotkeyTests
{
    private static readonly LibraryHotkeyContext Idle = default;

    [Fact]
    public void Ctrl_f_focuses_search_even_while_typing()
    {
        var typing = Idle with { EditorFocused = true };

        Assert.Equal(LibraryHotkey.FocusSearch, LibraryHotkeys.Match(Key.F, KeyModifiers.Control, "f", typing));
        Assert.Equal(LibraryHotkey.FocusSearch, LibraryHotkeys.Match(Key.F, KeyModifiers.Control, "f", Idle));
    }

    [Fact]
    public void Typing_keeps_letters_and_enter_for_the_editor()
    {
        var typing = Idle with { EditorFocused = true };

        Assert.Equal(LibraryHotkey.None, LibraryHotkeys.Match(Key.Enter, KeyModifiers.None, null, typing));
        Assert.Equal(LibraryHotkey.None, LibraryHotkeys.Match(Key.Escape, KeyModifiers.None, null, typing));
        Assert.Equal(LibraryHotkey.RefreshLibrary, LibraryHotkeys.Match(Key.F5, KeyModifiers.None, null, typing));
    }

    [Fact]
    public void Search_enter_launches_and_escape_dismisses()
    {
        var search = Idle with { SearchFocused = true };

        Assert.Equal(LibraryHotkey.LaunchSelected, LibraryHotkeys.Match(Key.Enter, KeyModifiers.None, null, search));
        Assert.Equal(LibraryHotkey.Dismiss, LibraryHotkeys.Match(Key.Escape, KeyModifiers.None, null, search));
        Assert.Equal(LibraryHotkey.None, LibraryHotkeys.Match(Key.PageDown, KeyModifiers.None, null, search));
    }

    [Fact]
    public void Buttons_and_lists_keep_enter()
    {
        Assert.Equal(
            LibraryHotkey.None,
            LibraryHotkeys.Match(Key.Enter, KeyModifiers.None, null, Idle with { ButtonFocused = true }));
        Assert.Equal(
            LibraryHotkey.None,
            LibraryHotkeys.Match(Key.Enter, KeyModifiers.None, null, Idle with { ListFocused = true }));
    }

    [Fact]
    public void Popups_swallow_shortcuts()
    {
        var popup = Idle with { PopupOpen = true };

        Assert.Equal(LibraryHotkey.None, LibraryHotkeys.Match(Key.F, KeyModifiers.Control, "f", popup));
        Assert.Equal(LibraryHotkey.None, LibraryHotkeys.Match(Key.Escape, KeyModifiers.None, null, popup));
    }

    [Theory]
    [InlineData(Key.R, LibraryHotkey.RefreshLibrary)]
    [InlineData(Key.N, LibraryHotkey.AddCustomGame)]
    [InlineData(Key.D, LibraryHotkey.ToggleFavorite)]
    [InlineData(Key.D1, LibraryHotkey.SetGridView)]
    [InlineData(Key.D2, LibraryHotkey.SetListView)]
    [InlineData(Key.OemComma, LibraryHotkey.OpenSettings)]
    public void Ctrl_shortcuts_map_library_actions(Key key, LibraryHotkey expected)
    {
        Assert.Equal(expected, LibraryHotkeys.Match(key, KeyModifiers.Control, null, Idle));
    }

    [Fact]
    public void Comma_symbol_opens_settings()
    {
        Assert.Equal(
            LibraryHotkey.OpenSettings,
            LibraryHotkeys.Match(Key.OemPeriod, KeyModifiers.Control, ",", Idle));
    }

    [Fact]
    public void Escape_clears_search_before_closing_the_detail()
    {
        Assert.Equal(LibraryDismiss.ClearSearch, LibraryHotkeys.ResolveDismiss(true, false, true));
        Assert.Equal(LibraryDismiss.ClearSearch, LibraryHotkeys.ResolveDismiss(false, true, true));
        Assert.Equal(LibraryDismiss.CloseDetail, LibraryHotkeys.ResolveDismiss(false, false, true));
        Assert.Equal(LibraryDismiss.None, LibraryHotkeys.ResolveDismiss(false, false, false));
    }
}
