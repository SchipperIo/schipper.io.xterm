namespace Schipper.Io.Xterm.Layout;

/// <summary>The line style used to draw a box border.</summary>
public enum BorderStyle
{
    /// <summary>No border glyphs (draws nothing).</summary>
    None,

    /// <summary>Single light lines: <c>┌─┐│└┘</c>.</summary>
    Single,

    /// <summary>Double lines: <c>╔═╗║╚╝</c>.</summary>
    Double,

    /// <summary>Single lines with rounded corners: <c>╭─╮│╰╯</c>.</summary>
    Rounded,

    /// <summary>Heavy lines: <c>┏━┓┃┗┛</c>.</summary>
    Heavy,

    /// <summary>Plain ASCII fallback: <c>+-+|++</c>.</summary>
    Ascii,
}
