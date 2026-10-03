# Screen and glyphs

[Index](README.md)

The grid lives in `Schipper.Io.Xterm.Screen` and `Schipper.Io.Xterm.Primitives`. Colors are emitted as stored. This renderer has no `ColorDepth` parameter. Choose `Color.Basic`, `Color.Palette`, or `Color.Rgb` for the terminals you ship to.

## Color and cells

```csharp
using Schipper.Io.Xterm.Primitives;

Color ink = Color.Rgb(222, 238, 240);
Color paper = Color.Basic(BasicColor.Black);
Color indexed = Color.Palette(39);
Color terminal = Color.Default;

var cell = new Cell('A', ink, paper, CellAttributes.Bold);
Cell blank = Cell.Empty;
bool samePen = cell.HasSameStyle(blank);
```

`ColorKind` is `Default`, `Basic`, `Palette256`, or `TrueColor`. `AppendSgr` writes the SGR parameters for one side. `BasicColor` is `Black` through `White` and `BrightBlack` through `BrightWhite`.

`CellAttributes`: `Bold`, `Faint`, `Italic`, `Underline`, `Blink`, `Reverse`, `Hidden`, `Strikethrough`.

## ScreenBuffer

Coordinates are zero-based. `x` grows right, `y` grows down.

```csharp
using Schipper.Io.Xterm.Screen;

var screen = new ScreenBuffer(80, 24);
screen.Set(0, 0, 'X', ink, paper);
screen.DrawText(0, 1, "hello", ink, paper);
screen.DrawHorizontalLine(0, 2, 10, Glyphs.Horizontal, ink, paper);
screen.DrawVerticalLine(0, 0, 4, Glyphs.Vertical, ink, paper);
screen.Fill(Cell.Empty);
screen.Resize(100, 30); // keeps the overlap, clears the new cells
ScreenBuffer copy = screen.Clone();
screen.Blit(copy, destX: 2, destY: 2);
```

`Set` drops a point outside the buffer and replaces C0/C1 controls and lone surrogates with U+FFFD before they are stored. `DrawText` walks Unicode runes, not UTF-16 units: a fullwidth glyph occupies two cells (the character plus a continuation column whose glyph is U+0000). `ScreenRenderer` skips that continuation and advances the terminal cursor by the display width, so the next ASCII cell is addressed with CUP when it would otherwise land under the wide glyph. The indexer requires an in-range point. `InBounds` tests that. `Cells` is the row-major span. `Clear` fills with `Cell.Empty`. Width and height may be zero.

## ScreenRenderer

| Method | Output |
| --- | --- |
| `RenderFull(current)` | Hides the cursor, resets SGR, clears the screen, paints every cell |
| `RenderComplete(current)` | Homes the cursor and rewrites every cell, with no erase |
| `RenderDiff(previous, current)` | Only the cells that differ. Empty when nothing changed |

`RenderDiff` falls back to `RenderFull` when `previous` is null or the size differs.

```csharp
string frame = ScreenRenderer.RenderDiff(previous, screen);
if (frame.Length > 0)
{
    await terminal.WriteAsync(frame);
    previous = screen.Clone();
}
```

Clone the buffer you sent. The next diff compares against that snapshot. After a resize, pass null as `previous` so the next frame is a full clear.

`RenderComplete` is the repair frame: every cell is rewritten in place, so a dropped byte cannot leave a stale glyph, and the terminal does not flash an erase.

Cursor addresses inside the stream are one-based. Cell coordinates on `ScreenBuffer` stay zero-based.

## Glyphs

`Glyphs` holds block and box characters: shades, half blocks, `Horizontal` / `Vertical`, the four single corners, and the double-line set (`DoubleHorizontal`, `DoubleTopLeft`, and the rest).

`GlyphsEx` adds the characters a denser UI tends to need.

| Group | Members |
| --- | --- |
| Ramps | `HorizontalEighths`, `VerticalEighths`, `ShadeRamp` |
| Quadrants | `QuadrantUpperLeft`, `QuadrantUpperRight`, `QuadrantLowerLeft`, `QuadrantLowerRight`, and the half blocks |
| Marks | Circles, squares, diamonds, `Check`, `Cross`, `Warning`, `Info`, stars |
| Arrows | `ArrowLeft` through `ArrowDown`, triangles, pointers, guillemets, chevrons |
| Spinners | `BrailleSpinner`, `DotsSpinner`, `CircleSpinner`, `LineSpinner` |

```csharp
char block = GlyphsEx.Braille(GlyphsEx.BrailleDot1 | GlyphsEx.BrailleDot4);
string bar = GlyphsEx.Bar(fraction: 0.5, width: 20);
string trend = GlyphsEx.Sparkline(samples);
char frame = GlyphsEx.SpinnerFrame(GlyphsEx.BrailleSpinner, tick);
```

`Braille` masks `dots` to eight bits and returns the Unicode braille character. `Bar` fills `width` cells from `HorizontalEighths` for `fraction` in 0..1. `Sparkline` maps a span of numbers onto `VerticalEighths`. `SpinnerFrame` indexes `frames` by `tick`.

## Cp437

`Cp437.Decode` and `Cp437.Encode` map all 256 bytes to Unicode without `System.Text.Encoding.CodePages`. `ToChar` and `FromChar` convert one value. Characters below U+0080 pass through `FromChar` unchanged, so ESC and newlines survive. Any other unmapped character becomes `Cp437.Substitute` (`?`).

C0 bytes decode to the classic CP437 dingbats. Treat those bytes as controls before decoding when the stream is a terminal, not a piece of art. Byte `0xFF` is U+00A0 (NBSP). Byte `0x00` is U+0020; ASCII space encodes as `0x20`.

`TerminalEncoding` (`Cp437` or `Utf8`) is a hint a handshake may store. `WriteAsync` sends the bytes you give it. Text written with `TerminalExtensions` is UTF-8.
