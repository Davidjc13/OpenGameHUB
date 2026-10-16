using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenGameHUB.ViewModels;

public enum ConsoleKeyboardKind
{
    Character,
    Space,
    Backspace,
    Done
}

public sealed partial class ConsoleKeyboardKey : ObservableObject
{
    public ConsoleKeyboardKey(string label, ConsoleKeyboardKind kind, string? character = null, bool isWide = false)
    {
        Label = label;
        Kind = kind;
        Character = character;
        IsWide = isWide;
    }

    public string Label { get; private set; }

    public ConsoleKeyboardKind Kind { get; }

    public string? Character { get; }

    public bool IsWide { get; }

    [ObservableProperty]
    private bool _isFocused;

    public void SetLabel(string label) => Label = label;
}

public readonly record struct ConsoleKeyboardResult(string Text, bool Close);

public sealed class ConsoleKeyboardRow(IReadOnlyList<ConsoleKeyboardKey> keys)
{
    public IReadOnlyList<ConsoleKeyboardKey> Keys { get; } = keys;
}

public sealed class ConsoleOnScreenKeyboard
{
    private readonly List<List<ConsoleKeyboardKey>> _rows = [];
    private int _row;
    private int _column;

    public IReadOnlyList<IReadOnlyList<ConsoleKeyboardKey>> Rows => _rows;

    public ConsoleKeyboardKey Focused => _rows[_row][_column];

    public void Rebuild(string spaceLabel, string backspaceLabel, string doneLabel)
    {
        _rows.Clear();
        AddRow(Chars("1234567890"));
        AddRow(Chars("QWERTYUIOP"));
        AddRow(Chars("ASDFGHJKL"));
        AddRow(Chars("ZXCVBNM"));
        _rows.Add(
        [
            new ConsoleKeyboardKey(spaceLabel, ConsoleKeyboardKind.Space, isWide: true),
            new ConsoleKeyboardKey(backspaceLabel, ConsoleKeyboardKind.Backspace, isWide: true),
            new ConsoleKeyboardKey(doneLabel, ConsoleKeyboardKind.Done, isWide: true)
        ]);
        _row = 0;
        _column = 0;
        RefreshFocus();
    }

    public void ResetFocus()
    {
        _row = 0;
        _column = 0;
        RefreshFocus();
    }

    public void Focus(ConsoleKeyboardKey key)
    {
        for (var row = 0; row < _rows.Count; row++)
        {
            var column = _rows[row].IndexOf(key);
            if (column < 0)
                continue;

            _row = row;
            _column = column;
            RefreshFocus();
            return;
        }
    }

    public bool Move(ConsoleInput input)
    {
        var rowDelta = 0;
        var columnDelta = 0;
        switch (input)
        {
            case ConsoleInput.MoveLeft:
                columnDelta = -1;
                break;
            case ConsoleInput.MoveRight:
                columnDelta = 1;
                break;
            case ConsoleInput.MoveUp:
            case ConsoleInput.PreviousCategory:
                rowDelta = -1;
                break;
            case ConsoleInput.MoveDown:
            case ConsoleInput.NextCategory:
                rowDelta = 1;
                break;
            default:
                return false;
        }

        if (rowDelta != 0)
            _row = Wrap(_row + rowDelta, _rows.Count);
        if (columnDelta != 0)
            _column = Wrap(_column + columnDelta, _rows[_row].Count);

        _column = Math.Clamp(_column, 0, _rows[_row].Count - 1);
        RefreshFocus();
        return true;
    }

    public ConsoleKeyboardResult Activate(string text)
    {
        var key = Focused;
        return key.Kind switch
        {
            ConsoleKeyboardKind.Space => new ConsoleKeyboardResult(text + " ", false),
            ConsoleKeyboardKind.Backspace => new ConsoleKeyboardResult(
                text.Length == 0 ? text : text[..^1],
                false),
            ConsoleKeyboardKind.Done => new ConsoleKeyboardResult(text, true),
            _ => new ConsoleKeyboardResult(text + (key.Character ?? key.Label), false)
        };
    }

    private void AddRow(IEnumerable<ConsoleKeyboardKey> keys) => _rows.Add([.. keys]);

    private static IEnumerable<ConsoleKeyboardKey> Chars(string letters) =>
        letters.Select(letter => new ConsoleKeyboardKey(letter.ToString(), ConsoleKeyboardKind.Character, letter.ToString()));

    private void RefreshFocus()
    {
        for (var row = 0; row < _rows.Count; row++)
        {
            for (var column = 0; column < _rows[row].Count; column++)
                _rows[row][column].IsFocused = row == _row && column == _column;
        }
    }

    private static int Wrap(int index, int count)
    {
        if (count <= 0)
            return 0;

        var next = index % count;
        return next < 0 ? next + count : next;
    }
}
