namespace Schipper.Io.Xterm.Layout;

/// <summary>The six glyphs that make up a box border for a given <see cref="BorderStyle"/>.</summary>
public readonly struct BorderGlyphs
{
    public BorderGlyphs(char horizontal, char vertical, char topLeft, char topRight, char bottomLeft, char bottomRight)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        TopLeft = topLeft;
        TopRight = topRight;
        BottomLeft = bottomLeft;
        BottomRight = bottomRight;
    }

    public char Horizontal { get; }
    public char Vertical { get; }
    public char TopLeft { get; }
    public char TopRight { get; }
    public char BottomLeft { get; }
    public char BottomRight { get; }

    /// <summary>The glyph set for a style. <see cref="BorderStyle.None"/> yields all-space glyphs.</summary>
    public static BorderGlyphs For(BorderStyle style) => style switch
    {
        BorderStyle.Single => new BorderGlyphs('─', '│', '┌', '┐', '└', '┘'),
        BorderStyle.Double => new BorderGlyphs('═', '║', '╔', '╗', '╚', '╝'),
        BorderStyle.Rounded => new BorderGlyphs('─', '│', '╭', '╮', '╰', '╯'),
        BorderStyle.Heavy => new BorderGlyphs('━', '┃', '┏', '┓', '┗', '┛'),
        BorderStyle.Ascii => new BorderGlyphs('-', '|', '+', '+', '+', '+'),
        _ => new BorderGlyphs(' ', ' ', ' ', ' ', ' ', ' '),
    };
}
