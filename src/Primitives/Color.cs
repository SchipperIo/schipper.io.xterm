using System.Text;

namespace Schipper.Io.Xterm.Primitives;

/// <summary>
/// An immutable terminal color that can be the default color, one of the 16 basic colors,
/// a 256-palette index, or a 24-bit RGB value. Knows how to emit its own SGR parameters.
/// </summary>
public readonly struct Color : IEquatable<Color>
{
    private readonly byte _a; // basic index / palette index / red
    private readonly byte _g;
    private readonly byte _b;

    private Color(ColorKind kind, byte a, byte g, byte b)
    {
        Kind = kind;
        _a = a;
        _g = g;
        _b = b;
    }

    public ColorKind Kind { get; }

    /// <summary>The terminal default color.</summary>
    public static Color Default => new(ColorKind.Default, 0, 0, 0);

    /// <summary>A color from the 16-color basic set.</summary>
    public static Color Basic(BasicColor color) => new(ColorKind.Basic, (byte)color, 0, 0);

    /// <summary>A color from the xterm 256-color palette.</summary>
    public static Color Palette(byte index) => new(ColorKind.Palette256, index, 0, 0);

    /// <summary>A 24-bit truecolor value.</summary>
    public static Color Rgb(byte r, byte g, byte b) => new(ColorKind.TrueColor, r, g, b);

    /// <summary>The basic color index (valid only when <see cref="Kind"/> is <see cref="ColorKind.Basic"/>).</summary>
    public BasicColor AsBasic => (BasicColor)_a;

    /// <summary>The palette index (valid only when <see cref="Kind"/> is <see cref="ColorKind.Palette256"/>).</summary>
    public byte PaletteIndex => _a;

    public byte R => _a;
    public byte G => _g;
    public byte B => _b;

    /// <summary>Appends the SGR parameters that select this color as a foreground or background.</summary>
    public void AppendSgr(StringBuilder sb, bool foreground)
    {
        switch (Kind)
        {
            case ColorKind.Default:
                sb.Append(foreground ? "39" : "49");
                break;

            case ColorKind.Basic:
            {
                int idx = _a & 0x0F;
                int code = idx < 8
                    ? (foreground ? 30 : 40) + idx
                    : (foreground ? 90 : 100) + (idx - 8);
                sb.Append(code);
                break;
            }

            case ColorKind.Palette256:
                sb.Append(foreground ? "38;5;" : "48;5;").Append(_a);
                break;

            case ColorKind.TrueColor:
                sb.Append(foreground ? "38;2;" : "48;2;")
                  .Append(_a).Append(';').Append(_g).Append(';').Append(_b);
                break;
        }
    }

    public bool Equals(Color other) =>
        Kind == other.Kind && _a == other._a && _g == other._g && _b == other._b;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);

    public override int GetHashCode() => HashCode.Combine((byte)Kind, _a, _g, _b);

    public static bool operator ==(Color left, Color right) => left.Equals(right);

    public static bool operator !=(Color left, Color right) => !left.Equals(right);
}
