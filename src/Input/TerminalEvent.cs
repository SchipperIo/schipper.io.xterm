namespace Schipper.Io.Xterm.Input;

/// <summary>
/// A single decoded terminal event: a key press, a mouse report, a paste block, or a focus change.
/// A value type discriminated by <see cref="Kind"/>; read the matching payload property.
/// </summary>
public readonly struct TerminalEvent
{
    private readonly KeyEvent _key;
    private readonly MouseEvent _mouse;
    private readonly string? _paste;
    private readonly bool _focused;

    private TerminalEvent(TerminalEventKind kind, KeyEvent key, MouseEvent mouse, string? paste, bool focused)
    {
        Kind = kind;
        _key = key;
        _mouse = mouse;
        _paste = paste;
        _focused = focused;
    }

    public TerminalEventKind Kind { get; }

    public static TerminalEvent FromKey(KeyEvent key) => new(TerminalEventKind.Key, key, default, null, false);

    public static TerminalEvent FromMouse(MouseEvent mouse) => new(TerminalEventKind.Mouse, default, mouse, null, false);

    public static TerminalEvent FromPaste(string text) => new(TerminalEventKind.Paste, default, default, text, false);

    public static TerminalEvent FromFocus(bool focused) => new(TerminalEventKind.Focus, default, default, null, focused);

    /// <summary>The key payload (valid when <see cref="Kind"/> is <see cref="TerminalEventKind.Key"/>).</summary>
    public KeyEvent Key => _key;

    /// <summary>The mouse payload (valid when <see cref="Kind"/> is <see cref="TerminalEventKind.Mouse"/>).</summary>
    public MouseEvent Mouse => _mouse;

    /// <summary>The pasted text (valid when <see cref="Kind"/> is <see cref="TerminalEventKind.Paste"/>).</summary>
    public string PasteText => _paste ?? string.Empty;

    /// <summary>True if the terminal gained focus (valid when <see cref="Kind"/> is <see cref="TerminalEventKind.Focus"/>).</summary>
    public bool Focused => _focused;

    public override string ToString() => Kind switch
    {
        TerminalEventKind.Key => $"Key {_key}",
        TerminalEventKind.Mouse => $"Mouse {_mouse}",
        TerminalEventKind.Paste => $"Paste [{PasteText.Length}]",
        TerminalEventKind.Focus => _focused ? "FocusIn" : "FocusOut",
        _ => "?",
    };
}
