namespace Schipper.Io.Xterm.Layout;

/// <summary>Which sides of a box to draw. Combine with <c>|</c>.</summary>
[Flags]
public enum BorderSides
{
    None = 0,
    Top = 1 << 0,
    Bottom = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    All = Top | Bottom | Left | Right,
}
