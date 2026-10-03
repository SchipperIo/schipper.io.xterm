using System.Text;

namespace Schipper.Io.Xterm.Primitives;

/// <summary>
/// Extended Unicode glyphs and small composing helpers that look great on modern (UTF-8) terminals
/// but have no CP437 equivalent: fine-grained block elements for smooth bars and sparklines, Braille
/// sub-cell "pixels", geometric/status symbols, arrows, and spinner frame sets. Everything here is
/// standard Unicode that renders in any monospace font — no Powerline/Nerd font required (if you do
/// have one, its separators live in the Private Use Area around U+E0B0).
/// </summary>
public static class GlyphsEx
{
    // ---- Block ramps (each character is one fill step; index 0 = empty) -------------------

    /// <summary>Left-to-right eighth blocks: <c>" ▏▎▍▌▋▊▉█"</c>. Index 0..8 = 0/8..8/8 filled from the left.</summary>
    public const string HorizontalEighths = " ▏▎▍▌▋▊▉█";

    /// <summary>Bottom-to-top eighth blocks: <c>" ▁▂▃▄▅▆▇█"</c>. Index 0..8 = 0/8..8/8 filled from the bottom.</summary>
    public const string VerticalEighths = " ▁▂▃▄▅▆▇█";

    /// <summary>The CP437-style shade ramp plus full block: <c>" ░▒▓█"</c> (0..4 = empty..solid).</summary>
    public const string ShadeRamp = " ░▒▓█";

    // ---- Quadrant blocks (2x2 sub-cell) ---------------------------------------------------

    public const char QuadrantUpperLeft = '▘';
    public const char QuadrantUpperRight = '▝';
    public const char QuadrantLowerLeft = '▖';
    public const char QuadrantLowerRight = '▗';
    public const char QuadrantUpperHalf = '▀';
    public const char QuadrantLowerHalf = '▄';
    public const char QuadrantLeftHalf = '▌';
    public const char QuadrantRightHalf = '▐';

    // ---- Geometric / status symbols -------------------------------------------------------

    public const char CircleFilled = '●';
    public const char CircleEmpty = '○';
    public const char CircleHalf = '◐';
    public const char SquareFilled = '■';
    public const char SquareEmpty = '□';
    public const char DiamondFilled = '◆';
    public const char DiamondEmpty = '◇';
    public const char Dot = '•';
    public const char MiddleDot = '·';
    public const char Ellipsis = '…';
    public const char Star = '★';
    public const char StarEmpty = '☆';

    public const char Check = '✓';
    public const char CheckHeavy = '✔';
    public const char Cross = '✗';
    public const char CrossHeavy = '✘';
    public const char Warning = '⚠';
    public const char Info = 'ⓘ';

    // ---- Arrows / pointers ----------------------------------------------------------------

    public const char ArrowLeft = '←';
    public const char ArrowUp = '↑';
    public const char ArrowRight = '→';
    public const char ArrowDown = '↓';
    public const char ArrowLeftRight = '↔';
    public const char ArrowUpDown = '↕';
    public const char Return = '↵';

    public const char TriangleRight = '▶';
    public const char TriangleLeft = '◀';
    public const char TriangleUp = '▲';
    public const char TriangleDown = '▼';
    public const char PointerRight = '▸';
    public const char PointerLeft = '◂';
    public const char PointerUp = '▴';
    public const char PointerDown = '▾';

    public const char GuillemetLeft = '«';
    public const char GuillemetRight = '»';
    public const char ChevronLeft = '‹';
    public const char ChevronRight = '›';

    // ---- Spinner frame sets (each character is one frame; advance per tick) ----------------

    /// <summary>Smooth Braille spinner: <c>⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏</c>.</summary>
    public const string BrailleSpinner = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

    /// <summary>Chunky Braille spinner: <c>⣾⣽⣻⢿⡿⣟⣯⣷</c>.</summary>
    public const string DotsSpinner = "⣾⣽⣻⢿⡿⣟⣯⣷";

    /// <summary>Rotating half-circle spinner: <c>◐◓◑◒</c>.</summary>
    public const string CircleSpinner = "◐◓◑◒";

    /// <summary>ASCII line spinner: <c>|/-\</c> (works even without Unicode).</summary>
    public const string LineSpinner = "|/-\\";

    // ---- Braille dot bits (compose a 2x4 cell). Layout: (1,4)/(2,5)/(3,6)/(7,8) ------------

    public const int BrailleDot1 = 0x01;
    public const int BrailleDot2 = 0x02;
    public const int BrailleDot3 = 0x04;
    public const int BrailleDot4 = 0x08;
    public const int BrailleDot5 = 0x10;
    public const int BrailleDot6 = 0x20;
    public const int BrailleDot7 = 0x40;
    public const int BrailleDot8 = 0x80;

    /// <summary>
    /// Composes one Braille cell (U+2800–U+28FF) from an 8-bit dot mask — a 2×4 grid of sub-cell
    /// "pixels". Combine the <c>BrailleDot*</c> constants: dots are laid out 1/4, 2/5, 3/6, 7/8 by row.
    /// </summary>
    public static char Braille(int dots) => (char)(0x2800 + (dots & 0xFF));

    /// <summary>
    /// Renders a smooth horizontal progress bar <paramref name="width"/> cells wide, filled to
    /// <paramref name="fraction"/> (clamped to 0..1) with eighth-block precision — far finer than a
    /// whole-cell bar. Returns a string exactly <paramref name="width"/> characters long.
    /// </summary>
    public static string Bar(double fraction, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        double clamped = Math.Clamp(fraction, 0.0, 1.0);
        int eighths = (int)Math.Round(clamped * width * 8, MidpointRounding.AwayFromZero);
        int full = eighths / 8;
        int remainder = eighths % 8;

        var sb = new StringBuilder(width);
        sb.Append('█', Math.Min(full, width));
        if (full < width && remainder > 0)
        {
            sb.Append(HorizontalEighths[remainder]);
        }

        while (sb.Length < width)
        {
            sb.Append(' ');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a one-line sparkline from <paramref name="values"/> using the eight vertical block
    /// levels (<c>▁▂▃▄▅▆▇█</c>), auto-scaling between the min and max. Returns one character per value.
    /// </summary>
    public static string Sparkline(ReadOnlySpan<double> values)
    {
        if (values.Length == 0)
        {
            return string.Empty;
        }

        double min = values[0];
        double max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < min) min = values[i];
            if (values[i] > max) max = values[i];
        }

        double range = max - min;
        var sb = new StringBuilder(values.Length);
        foreach (double v in values)
        {
            int level = range <= 0 ? 4 : 1 + (int)((v - min) / range * 7);
            sb.Append(VerticalEighths[Math.Clamp(level, 1, 8)]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Picks the frame for the given <paramref name="tick"/> from a spinner frame string (one
    /// character per frame). Negative ticks are handled. Pair with <see cref="BrailleSpinner"/> et al.
    /// </summary>
    public static char SpinnerFrame(string frames, long tick)
    {
        ArgumentException.ThrowIfNullOrEmpty(frames);
        int n = frames.Length;
        int index = (int)(((tick % n) + n) % n);
        return frames[index];
    }
}
