using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Layout;

/// <summary>
/// Stateless immediate-mode painters over a caller-owned <see cref="ScreenBuffer"/>: fills, boxes,
/// aligned text, header/footer bars, a dim-and-panel modal, and centered splash content. Everything
/// clips silently to the buffer (and, where given, to the target <see cref="Rect"/>).
/// </summary>
public static class Draw
{
    /// <summary>Fills <paramref name="rect"/> with one styled glyph.</summary>
    public static void Fill(ScreenBuffer buffer, Rect rect, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var cell = new Cell(glyph, foreground, background, attributes);
        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            for (int x = rect.X; x < rect.Right; x++)
            {
                buffer.Set(x, y, cell);
            }
        }
    }

    /// <summary>Fills <paramref name="rect"/> with blanks in the given background color.</summary>
    public static void FillBackground(ScreenBuffer buffer, Rect rect, Color background) =>
        Fill(buffer, rect, ' ', Color.Default, background);

    /// <summary>Draws a box border around <paramref name="rect"/> on the requested <paramref name="sides"/>.</summary>
    public static void Box(
        ScreenBuffer buffer,
        Rect rect,
        BorderStyle style,
        Color foreground,
        Color background,
        BorderSides sides = BorderSides.All,
        CellAttributes attributes = CellAttributes.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (rect.IsEmpty || style == BorderStyle.None || sides == BorderSides.None)
        {
            return;
        }

        BorderGlyphs g = BorderGlyphs.For(style);
        int x0 = rect.X;
        int y0 = rect.Y;
        int x1 = rect.Right - 1;
        int y1 = rect.Bottom - 1;

        bool top = (sides & BorderSides.Top) != 0;
        bool bottom = (sides & BorderSides.Bottom) != 0;
        bool left = (sides & BorderSides.Left) != 0;
        bool right = (sides & BorderSides.Right) != 0;

        if (top) { for (int x = x0; x <= x1; x++) buffer.Set(x, y0, g.Horizontal, foreground, background, attributes); }
        if (bottom) { for (int x = x0; x <= x1; x++) buffer.Set(x, y1, g.Horizontal, foreground, background, attributes); }
        if (left) { for (int y = y0; y <= y1; y++) buffer.Set(x0, y, g.Vertical, foreground, background, attributes); }
        if (right) { for (int y = y0; y <= y1; y++) buffer.Set(x1, y, g.Vertical, foreground, background, attributes); }

        if (top && left) buffer.Set(x0, y0, g.TopLeft, foreground, background, attributes);
        if (top && right) buffer.Set(x1, y0, g.TopRight, foreground, background, attributes);
        if (bottom && left) buffer.Set(x0, y1, g.BottomLeft, foreground, background, attributes);
        if (bottom && right) buffer.Set(x1, y1, g.BottomRight, foreground, background, attributes);
    }

    /// <summary>Draws one aligned line of text on the first row of <paramref name="rect"/>, clipped to it.</summary>
    public static void Text(
        ScreenBuffer buffer,
        Rect rect,
        string text,
        Color foreground,
        Color background,
        TextAlign align = TextAlign.Left,
        CellAttributes attributes = CellAttributes.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        PutText(buffer, rect.X, rect.Width, rect.Y, text, foreground, background, attributes, align);
    }

    /// <summary>
    /// Paints a header bar: fills <paramref name="rect"/> with the background, then draws
    /// <paramref name="title"/> on its vertically-centered row.
    /// </summary>
    public static void Header(
        ScreenBuffer buffer,
        Rect rect,
        string title,
        Color foreground,
        Color background,
        TextAlign align = TextAlign.Center,
        CellAttributes attributes = CellAttributes.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        FillBackground(buffer, rect, background);
        if (rect.IsEmpty)
        {
            return;
        }

        int row = rect.Y + ((rect.Height - 1) / 2);
        PutText(buffer, rect.X, rect.Width, row, title, foreground, background, attributes, align);
    }

    /// <summary>Paints a footer bar — identical to <see cref="Header"/> but conventionally at the bottom.</summary>
    public static void Footer(
        ScreenBuffer buffer,
        Rect rect,
        string text,
        Color foreground,
        Color background,
        TextAlign align = TextAlign.Center,
        CellAttributes attributes = CellAttributes.None) =>
        Header(buffer, rect, text, foreground, background, align, attributes);

    /// <summary>
    /// Dims an area (whole buffer by default) by adding <see cref="CellAttributes.Faint"/> to every
    /// cell — the conventional backdrop behind a modal.
    /// </summary>
    public static void DimBackground(ScreenBuffer buffer, Rect? area = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Rect r = area ?? new Rect(0, 0, buffer.Width, buffer.Height);
        for (int y = r.Y; y < r.Bottom; y++)
        {
            for (int x = r.X; x < r.Right; x++)
            {
                if (!buffer.InBounds(x, y))
                {
                    continue;
                }

                Cell c = buffer[x, y];
                buffer.Set(x, y, new Cell(c.Glyph, c.Foreground, c.Background, c.Attributes | CellAttributes.Faint));
            }
        }
    }

    /// <summary>
    /// Renders a modal: dims the whole buffer, clears the <paramref name="panel"/> interior to the
    /// panel background, draws its border, and (optionally) a <paramref name="title"/> on the top edge.
    /// </summary>
    public static void Modal(
        ScreenBuffer buffer,
        Rect panel,
        BorderStyle style,
        Color foreground,
        Color background,
        string? title = null,
        CellAttributes attributes = CellAttributes.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        DimBackground(buffer);
        FillBackground(buffer, panel, background);
        Box(buffer, panel, style, foreground, background, BorderSides.All, attributes);

        if (!string.IsNullOrEmpty(title) && panel.Width > 2)
        {
            PutText(buffer, panel.X + 1, panel.Width - 2, panel.Y, $" {title} ", foreground, background, attributes, TextAlign.Center);
        }
    }

    /// <summary>Centers <paramref name="content"/> in the buffer and blits it (a splash screen).</summary>
    public static void Splash(ScreenBuffer buffer, ScreenBuffer content)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(content);
        Rect target = new Rect(0, 0, buffer.Width, buffer.Height).Centered(content.Width, content.Height);
        buffer.Blit(content, target.X, target.Y);
    }

    /// <summary>Centers a block of text lines in the buffer (a text splash screen).</summary>
    public static void Splash(ScreenBuffer buffer, IReadOnlyList<string> lines, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            return;
        }

        int widest = 0;
        foreach (string line in lines)
        {
            widest = Math.Max(widest, line.Length);
        }

        Rect block = new Rect(0, 0, buffer.Width, buffer.Height).Centered(widest, lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            PutText(buffer, block.X, block.Width, block.Y + i, lines[i], foreground, background, attributes, TextAlign.Center);
        }
    }

    // Draws one line of text into [left, left+width) on the given row, aligned and clipped to width.
    private static void PutText(ScreenBuffer buffer, int left, int width, int row, string text, Color fg, Color bg, CellAttributes attrs, TextAlign align)
    {
        if (width <= 0 || string.IsNullOrEmpty(text))
        {
            return;
        }

        string shown = CellPaint.Clip(text, width);
        int shownWidth = CellPaint.Measure(shown);
        int startX = align switch
        {
            TextAlign.Center => left + ((width - shownWidth) / 2),
            TextAlign.Right => left + width - shownWidth,
            _ => left,
        };

        buffer.DrawText(startX, row, shown, fg, bg, attrs);
    }
}
