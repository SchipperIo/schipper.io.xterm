# Layout

[Index](README.md)

`ScreenLayout` solves rectangles. Each `Region` owns a `Surface` you paint. `Compose` blits every region into the `ScreenBuffer` you render.

## Building a layout

```csharp
using Schipper.Io.Xterm.Layout;
using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;

var layout = new ScreenLayout(width: 80, height: 24);
Region header = layout.AddTop(1, name: "header");
Region footer = layout.AddBottom(2, BorderStyle.Single, BorderSides.Top, name: "footer");
Region body = layout.AddFill(BorderStyle.Rounded, name: "body");

ScreenLayout full = ScreenLayout.Fullscreen(80, 24); // one fill region named "content"
```

`AddTop` and `AddBottom` place a fixed band. A later top band sits under the earlier ones. A later bottom band sits above the earlier ones. `AddFill` takes the rectangle that remains. Heights passed to `AddTop` and `AddBottom` include the border.

`layout["header"]` returns the region with that name. `Find` returns null when the name is missing. `Regions` is the band list. `Area` is the outer `Rect`.

`Resize(columns, rows)` runs when the terminal size changes, then paint the new surfaces. `RequestHeight` on a top, bottom, or modal region asks for a new outer height and returns the height actually granted. `RequestWidth` does the same for a modal's width.

## Region

| Member | Meaning |
| --- | --- |
| `Name` | The name passed to `Add*` or `PushModal` |
| `Placement` | `Top`, `Bottom`, `Fill`, or `Modal` |
| `Bounds` | Outer rectangle in screen coordinates |
| `Content` | Inner rectangle, inside the border |
| `ContentWidth`, `ContentHeight`, `HasContent` | The inner size |
| `Surface` | A `ScreenBuffer` of the content size, including `0`×N when the border consumes the band. Recreated when the layout resolves |
| `Border`, `BorderSides` | Line style and which edges are drawn |
| `BorderForeground`, `BorderBackground` | Border colors. Default `Color.Default` |

Paint `Surface` in its own coordinates. `(0, 0)` is the content origin, not the screen origin. `Compose` places it at `Content`.

`BorderStyle`: `None`, `Single`, `Double`, `Rounded`, `Heavy`, `Ascii`.

`BorderSides` is a flags enum: `None`, `Top`, `Bottom`, `Left`, `Right`, `All`.

`BorderGlyphs.For(style)` returns the six characters for that style.

## Modals

```csharp
Region dialog = layout.PushModal(40, 8, BorderStyle.Double, name: "confirm");
Draw.Text(dialog.Surface, new Rect(1, 1, dialog.ContentWidth - 2, 1),
    "Delete this file?", Color.Default, Color.Default);
layout.Compose(screen);

bool closed = layout.PopModal();
```

`PushModal` centers a region of the requested size and stacks it. `ActiveModal` is the top of the stack. `Modals` is the whole stack. `PopModal` removes the top and returns false when the stack was empty.

`Compose` blits the bands, then dims the target and blits each modal. The dim is `CellAttributes.Faint` on the cells already in the target.

## Rect

```csharp
var page = new Rect(0, 0, screen.Width, screen.Height);
bool inside = page.Contains(x, y);
Rect padded = page.Inset(1);
(Rect head, Rect rest) = page.SplitTop(1);
(Rect body, Rect foot) = page.SplitBottom(1);
Rect dialog = page.Centered(40, 8);
```

`Right` and `Bottom` are exclusive. `IsEmpty` is true when `Width` or `Height` is less than or equal to zero.

## Draw

`Schipper.Io.Xterm.Layout.Draw` paints a `ScreenBuffer` inside a `Rect`.

| Method | Effect |
| --- | --- |
| `Fill` | A glyph and colors across the rectangle |
| `FillBackground` | Spaces in a background color |
| `Box` | A border in a `BorderStyle` and `BorderSides` |
| `Text` | A line aligned `Left`, `Center`, or `Right` |
| `Header` / `Footer` | A filled bar and one line of text. `Footer` calls `Header` |
| `DimBackground` | Adds `Faint` to every cell in the area, or the whole buffer |
| `Modal` | Dims the buffer, fills a panel, draws its border, and centers an optional title on the top edge |
| `Splash` | Centers another `ScreenBuffer`, or a list of lines |

`TextAlign` defaults to `Left` for `Text` and `Center` for `Header` and `Footer`.

`Draw.Modal` is the immediate-mode panel. `ScreenLayout.PushModal` is the retained one. Use the layout when the panel lives across frames and should move on resize. Use `Draw.Modal` when you are painting a single buffer by hand.
