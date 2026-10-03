namespace Schipper.Io.Xterm.Input;

/// <summary>A decoded key press: a logical key plus, for <see cref="Key.Char"/>, the character.</summary>
public readonly struct KeyEvent
{
    public KeyEvent(Key key, char character = '\0', bool ctrl = false, bool alt = false, bool shift = false)
    {
        Key = key;
        Char = character;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }

    public Key Key { get; }
    public char Char { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }

    public static KeyEvent FromChar(char c, bool alt = false) => new(Key.Char, c, alt: alt);

    public bool IsChar => Key == Key.Char;

    public override string ToString() =>
        Key == Key.Char ? $"Char '{Char}'{(Alt ? " +Alt" : string.Empty)}" : Key.ToString();
}
