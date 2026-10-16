using OpenGameHUB.ViewModels;

namespace OpenGameHUB.Tests;

public sealed class ConsoleOnScreenKeyboardTests
{
    [Fact]
    public void A_types_the_focused_character()
    {
        var keyboard = Create();

        var result = keyboard.Activate(string.Empty);

        Assert.Equal("1", result.Text);
        Assert.False(result.Close);
    }

    [Fact]
    public void Dpad_moves_between_keys_and_wraps()
    {
        var keyboard = Create();

        keyboard.Move(ConsoleInput.MoveRight);
        Assert.Equal("2", keyboard.Focused.Label);

        keyboard.Move(ConsoleInput.MoveLeft);
        keyboard.Move(ConsoleInput.MoveLeft);
        Assert.Equal("0", keyboard.Focused.Label);
    }

    [Fact]
    public void Backspace_and_done_edit_the_query()
    {
        var keyboard = Create();
        keyboard.Move(ConsoleInput.MoveDown);
        keyboard.Move(ConsoleInput.MoveDown);
        keyboard.Move(ConsoleInput.MoveDown);
        keyboard.Move(ConsoleInput.MoveDown);

        var spaced = keyboard.Activate("LoL");
        Assert.Equal("LoL ", spaced.Text);

        keyboard.Move(ConsoleInput.MoveRight);
        var deleted = keyboard.Activate("LoL ");
        Assert.Equal("LoL", deleted.Text);

        keyboard.Move(ConsoleInput.MoveRight);
        var done = keyboard.Activate("LoL");
        Assert.True(done.Close);
        Assert.Equal("LoL", done.Text);
    }

    private static ConsoleOnScreenKeyboard Create()
    {
        var keyboard = new ConsoleOnScreenKeyboard();
        keyboard.Rebuild("Space", "Delete", "Done");
        return keyboard;
    }
}
