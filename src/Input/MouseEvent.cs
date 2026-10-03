namespace Schipper.Io.Xterm.Input;

/// <summary>
/// A decoded mouse report. <see cref="X"/>/<see cref="Y"/> are <b>zero-based</b> cell coordinates
/// (the protocol's 1-based values minus one) so they line up with <see cref="ScreenBuffer"/> and
/// <see cref="Rect"/>.
/// </summary>
public readonly struct MouseEvent
{
    public MouseEvent(MouseButton button, MouseAction action, int x, int y, bool ctrl = false, bool alt = false, bool shift = false)
    {
        Button = button;
        Action = action;
        X = x;
        Y = y;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }

    public MouseButton Button { get; }
    public MouseAction Action { get; }
    public int X { get; }
    public int Y { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }

    public override string ToString() => $"{Action} {Button} @ ({X},{Y})";
}
