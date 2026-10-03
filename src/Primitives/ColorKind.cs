namespace Schipper.Io.Xterm.Primitives;

/// <summary>How a <see cref="Color"/> value should be interpreted.</summary>
public enum ColorKind : byte
{
    /// <summary>The terminal's default foreground/background (SGR 39/49).</summary>
    Default = 0,

    /// <summary>One of the 16 <see cref="BasicColor"/> values.</summary>
    Basic = 1,

    /// <summary>An index into the xterm 256-color palette.</summary>
    Palette256 = 2,

    /// <summary>A 24-bit truecolor RGB value.</summary>
    TrueColor = 3,
}
