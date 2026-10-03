using System.Text;
using Schipper.Io.Xterm.Primitives;

namespace Schipper.Io.Xterm.Screen;

/// <summary>
/// A fixed-size grid of <see cref="Cell"/>s with simple drawing primitives. Coordinates are
/// zero-based with (0,0) at the top-left. Drawing operations clip silently to the bounds.
/// </summary>
public sealed class ScreenBuffer
{
    private Cell[] _cells;

    public ScreenBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        long cells = (long)width * height;
        if (cells > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width × height overflows the cell store.");
        }

        Width = width;
        Height = height;
        _cells = new Cell[(int)cells];
        Clear();
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public Cell this[int x, int y]
    {
        get => _cells[Index(x, y)];
        set => _cells[Index(x, y)] = value;
    }

    /// <summary>Direct read-only view of the backing store, row-major.</summary>
    public ReadOnlySpan<Cell> Cells => _cells;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Fills the whole buffer with a blank cell using default colors.</summary>
    public void Clear() => Fill(Cell.Empty);

    /// <summary>Fills the whole buffer with the given cell.</summary>
    public void Fill(in Cell cell)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i] = cell;
        }
    }

    /// <summary>Resizes the buffer, preserving overlapping content and clearing new area.</summary>
    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        long cells = (long)width * height;
        if (cells > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width × height overflows the cell store.");
        }
        if (width == Width && height == Height)
        {
            return;
        }

        var next = new Cell[(int)cells];
        for (int i = 0; i < next.Length; i++)
        {
            next[i] = Cell.Empty;
        }

        int copyW = Math.Min(width, Width);
        int copyH = Math.Min(height, Height);
        for (int y = 0; y < copyH; y++)
        {
            for (int x = 0; x < copyW; x++)
            {
                next[(y * width) + x] = _cells[(y * Width) + x];
            }
        }

        _cells = next;
        Width = width;
        Height = height;
    }

    public void Set(int x, int y, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        if (InBounds(x, y))
        {
            _cells[Index(x, y)] = new Cell(CellPaint.Sanitize(glyph), foreground, background, attributes);
        }
    }

    public void Set(int x, int y, in Cell cell)
    {
        if (InBounds(x, y))
        {
            char glyph = CellPaint.IsContinuation(cell.Glyph)
                ? cell.Glyph
                : CellPaint.Sanitize(cell.Glyph);
            _cells[Index(x, y)] = new Cell(glyph, cell.Foreground, cell.Background, cell.Attributes);
        }
    }

    /// <summary>
    /// Writes a string left-to-right starting at (x,y), clipping at the right edge. Iterates Unicode
    /// runes: C0/C1 and lone surrogates become U+FFFD, and a wide rune occupies two cells (glyph plus
    /// a continuation column the renderer skips).
    /// </summary>
    public void DrawText(int x, int y, string text, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        if (string.IsNullOrEmpty(text) || (uint)y >= (uint)Height)
        {
            return;
        }

        int cx = x;
        foreach (Rune rune in text.AsSpan().EnumerateRunes())
        {
            char glyph = CellPaint.SanitizeRune(rune);
            int width = CellPaint.Width(new Rune(glyph));
            if (width <= 0)
            {
                continue;
            }

            if (cx >= Width)
            {
                break;
            }

            if (width == 2 && cx + 1 >= Width)
            {
                break;
            }

            if (cx >= 0)
            {
                _cells[Index(cx, y)] = new Cell(glyph, foreground, background, attributes);
                if (width == 2)
                {
                    _cells[Index(cx + 1, y)] = new Cell(CellPaint.ContinuationGlyph, foreground, background, attributes);
                }
            }

            cx += width;
        }
    }

    /// <summary>Draws a horizontal run of one glyph.</summary>
    public void DrawHorizontalLine(int x, int y, int length, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        for (int i = 0; i < length; i++)
        {
            Set(x + i, y, glyph, foreground, background, attributes);
        }
    }

    /// <summary>Draws a vertical run of one glyph.</summary>
    public void DrawVerticalLine(int x, int y, int length, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        for (int i = 0; i < length; i++)
        {
            Set(x, y + i, glyph, foreground, background, attributes);
        }
    }

    /// <summary>Copies another buffer onto this one at the given offset (used for compositing/blits).</summary>
    public void Blit(ScreenBuffer source, int destX, int destY)
    {
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Set(destX + x, destY + y, source[x, y]);
            }
        }
    }

    /// <summary>Creates an independent copy of this buffer.</summary>
    public ScreenBuffer Clone()
    {
        var clone = new ScreenBuffer(Width, Height);
        _cells.AsSpan().CopyTo(clone._cells);
        return clone;
    }

    private int Index(int x, int y)
    {
        if (!InBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside {Width}x{Height}.");
        }

        return (y * Width) + x;
    }
}
