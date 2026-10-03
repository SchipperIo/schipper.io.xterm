# Schipper.Io.Xterm

A self-contained terminal toolkit for .NET 10. One package holds the cell grid, the diffing renderer, color, keyboard and mouse decoding, layout, and a console transport. It does not reference `Schipper.Io.Ansi`.

The runnable sample is [`demo/`](../demo/README.md), a two-pane file browser.

## Guides

| Guide | What it covers |
| --- | --- |
| [Getting started](getting-started.md) | `ConsoleTerminal`, `XtermSession`, and a frame loop |
| [Input and sequences](input-and-emit.md) | `XtermInput`, `Vt`, mouse, paste, focus |
| [Layout](layout.md) | `ScreenLayout`, regions, `Draw` |
| [Screen and glyphs](screen-and-glyphs.md) | `ScreenBuffer`, `ScreenRenderer`, `Glyphs`, `GlyphsEx` |

## Namespaces

| Namespace | Types |
| --- | --- |
| `Schipper.Io.Xterm.Transport` | `ITerminal`, `ConsoleTerminal`, `TerminalExtensions`, `TerminalEncoding` |
| `Schipper.Io.Xterm.Emit` | `Vt`, `Ansi`, `XtermSession`, `XtermSessionOptions`, `MouseMode`, `CursorStyle` |
| `Schipper.Io.Xterm.Input` | `XtermInput`, `InputParser`, `TerminalEvent`, `KeyEvent`, `MouseEvent` |
| `Schipper.Io.Xterm.Screen` | `ScreenBuffer`, `ScreenRenderer` |
| `Schipper.Io.Xterm.Primitives` | `Cell`, `Color`, `CellAttributes`, `Glyphs`, `GlyphsEx`, `Cp437` |
| `Schipper.Io.Xterm.Layout` | `ScreenLayout`, `Region`, `Rect`, `Draw`, border and alignment enums |

`Ansi` in `Schipper.Io.Xterm.Emit` is this package's sequence helper. It is not the `Schipper.Io.Ansi` package.

The wire encoding is UTF-8. `TerminalEncoding.Cp437` is a handshake hint. CP437 bytes are an on-disk art format via `Cp437`.

## Package

```xml
<PackageReference Include="Schipper.Io.Xterm" Version="0.1.0-dev" />
```

Target framework: `net10.0`. The package has no dependencies. `PublishAot` is supported. `ConsoleTerminal` uses blittable `LibraryImport` and touches a real console only for its own lifetime.
