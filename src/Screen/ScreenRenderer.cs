using System.Text;
using Schipper.Io.Xterm.Emit;
using Schipper.Io.Xterm.Primitives;

namespace Schipper.Io.Xterm.Screen;

/// <summary>
/// Turns a <see cref="ScreenBuffer"/> into the ANSI byte stream needed to display it, emitting a
/// minimal diff against a previously rendered buffer. Cursor moves and SGR changes are coalesced so
/// only cells that actually changed are repainted.
/// </summary>
public static class ScreenRenderer
{
    /// <summary>Renders the entire buffer after clearing the screen.</summary>
    public static string RenderFull(ScreenBuffer current)
    {
        var sb = new StringBuilder(current.Width * current.Height);
        Ansi.HideCursor(sb);
        Ansi.ResetGraphics(sb);
        Ansi.ClearScreen(sb);
        Ansi.CursorTo(sb, 1, 1);
        AppendChanges(sb, previous: null, current);
        Ansi.ResetGraphics(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Renders the whole buffer by rewriting every cell, homing the cursor first but <b>without</b>
    /// clearing the screen. Because every cell is overwritten, the previous frame is fully replaced
    /// with no residue — and, unlike <see cref="RenderFull"/>, no erase flash. This is the full-frame
    /// path: robust against any drift in what is actually on screen (raw ANSI from a child program,
    /// dropped bytes, autowrap quirks) at the cost of sending the entire frame each time.
    /// </summary>
    public static string RenderComplete(ScreenBuffer current)
    {
        var sb = new StringBuilder(current.Width * current.Height);
        Ansi.ResetGraphics(sb);
        Ansi.CursorTo(sb, 1, 1);
        AppendChanges(sb, previous: null, current);
        Ansi.ResetGraphics(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Renders only the cells that differ from <paramref name="previous"/>. Falls back to a full
    /// render when the dimensions changed or there is no previous frame. Returns an empty string
    /// when nothing changed.
    /// </summary>
    public static string RenderDiff(ScreenBuffer? previous, ScreenBuffer current)
    {
        if (previous is null || previous.Width != current.Width || previous.Height != current.Height)
        {
            return RenderFull(current);
        }

        var sb = new StringBuilder();
        if (AppendChanges(sb, previous, current))
        {
            Ansi.ResetGraphics(sb);
        }

        return sb.ToString();
    }

    private static bool AppendChanges(StringBuilder sb, ScreenBuffer? previous, ScreenBuffer current)
    {
        bool wrote = false;
        bool penValid = false;
        Cell pen = default;

        // Expected terminal cursor position (1-based), -1 meaning "unknown, force a move".
        int curRow = -1;
        int curCol = -1;

        for (int y = 0; y < current.Height; y++)
        {
            for (int x = 0; x < current.Width; x++)
            {
                Cell cell = current[x, y];
                if (CellPaint.IsContinuation(cell.Glyph))
                {
                    continue;
                }

                if (previous is not null && cell == previous[x, y])
                {
                    continue;
                }

                int row = y + 1;
                int col = x + 1;

                if (curRow != row || curCol != col)
                {
                    Ansi.CursorTo(sb, row, col);
                    curRow = row;
                    curCol = col;
                }

                if (!penValid || !cell.HasSameStyle(pen))
                {
                    Ansi.SetGraphics(sb, cell);
                    pen = cell;
                    penValid = true;
                }

                char glyph = CellPaint.Sanitize(cell.Glyph);
                int displayWidth = CellPaint.Width(glyph);
                sb.Append(glyph);
                wrote = true;

                int advance = Math.Max(1, displayWidth);
                if (x + advance > current.Width - 1)
                {
                    curRow = -1;
                    curCol = -1;
                }
                else
                {
                    curCol = col + advance;
                }
            }
        }

        return wrote;
    }
}
