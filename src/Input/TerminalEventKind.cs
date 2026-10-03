namespace Schipper.Io.Xterm.Input;

/// <summary>Which kind of payload a <see cref="TerminalEvent"/> carries.</summary>
public enum TerminalEventKind
{
    Key,
    Mouse,
    Paste,
    Focus,
}
