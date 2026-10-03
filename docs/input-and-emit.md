# Input and sequences

[Index](README.md)

`XtermInput` decodes the byte stream into `TerminalEvent` values. `Vt` builds the sequences that ask the terminal to produce them.

## TerminalEvent

| `Kind` | Payload |
| --- | --- |
| `Key` | `Key` (`KeyEvent`) |
| `Mouse` | `Mouse` (`MouseEvent`) |
| `Paste` | `PasteText` |
| `Focus` | `Focused` |

```csharp
var input = new XtermInput();

foreach (TerminalEvent ev in input.Feed(bytes))
{
    switch (ev.Kind)
    {
        case TerminalEventKind.Key:
            KeyEvent key = ev.Key;
            break;
        case TerminalEventKind.Mouse:
            MouseEvent mouse = ev.Mouse; // X and Y are zero-based cells
            break;
        case TerminalEventKind.Paste:
            string pasted = ev.PasteText; // UTF-8 text from a bracketed paste
            break;
        case TerminalEventKind.Focus:
            bool focused = ev.Focused;
            break;
    }
}
```

`Feed` returns a new list for that call. `PendingCount` is the number of bytes held for an unfinished sequence. `PendingIsLoneEscape` is true when that hold is a single ESC. `Flush` turns a lone ESC into `Key.Escape`. A longer incomplete CSI, mouse report, or bracketed paste stays buffered. `XtermInput.Flush` forwards to the wrapped `InputParser` when its own buffer is empty, so a held CR or incomplete UTF-8 is not stranded. Call `Flush` when the read has gone idle (about 50–100 ms with no more bytes).

Mouse, paste, and focus events arrive only after the terminal was asked to send them. Set `MouseMode`, `BracketedPaste`, and `FocusReporting` on `XtermSessionOptions`, or emit the matching `Vt` sequences yourself.

Ordinary keystrokes go through the wrapped `InputParser`: UTF-8 characters (not CP437), controls, and CSI / SS3 arrows and function keys. A typed `é` is one `Key.Char`. Bytes `00` and `1C`–`1F` are Ctrl+Space, `\`, `]`, `^`, and `_`. A CR split from a following LF across two reads is still one Enter. `XtermInput` adds SGR mouse (1006), bracketed paste, focus in and out, CSI-u, and xterm modifyOtherKeys form 2 (`CSI 27;mod;code~`). SGR buttons 8–11 (bit 7) are `MouseButton.None`, not Left. Wheel ticks stay `WheelUp` / `WheelDown`.

## Keys

`Key` is `Char`, `Enter`, `Escape`, `Backspace`, `Tab`, the arrows, `Home`, `End`, `PageUp`, `PageDown`, `Insert`, `Delete`, and `F1` through `F12`. `KeyEvent` carries `Char`, `Ctrl`, `Alt`, and `Shift`. `IsChar` is true when `Key` is `Char`. `KeyEvent.FromChar` builds a character event.

`InputParser` is public for a loop that wants keys only. Its `Feed` also returns a fresh list. `Flush` has the same lone-ESC meaning.

## Mouse

`MouseButton`: `None`, `Left`, `Middle`, `Right`, `WheelUp`, `WheelDown`, `WheelLeft`, `WheelRight`.

`MouseAction`: `Press` (a button down, or a wheel tick), `Release`, `Move`.

`MouseEvent` also carries `Ctrl`, `Alt`, and `Shift`. `X` and `Y` are zero-based cell coordinates.

| `MouseMode` | DEC mode | Reports |
| --- | --- | --- |
| `Off` |  | No tracking |
| `Click` | 1000 | Press and release |
| `ButtonEvent` | 1002 | Press, release, and motion while a button is held |
| `AnyEvent` | 1003 | All motion |

`Vt.EnableMouse` turns on SGR 1006 encoding for the mode you pass. `Vt.DisableMouse` turns tracking off.

## Vt

Methods append to a `StringBuilder`.

| Method | Sequence |
| --- | --- |
| `SetTitle(sb, title)` | OSC 0, window title, BEL |
| `Hyperlink(sb, uri, text, id)` | OSC 8 around `text`. `id` groups spans into one link |
| `SetClipboard(sb, text, selection)` | OSC 52. `selection` defaults to `'c'` (clipboard). `'p'` is the primary selection. The text is base64 UTF-8 |
| `SetCursorStyle(sb, style)` | DECSCUSR |
| `BeginSync` / `EndSync` | DEC 2026 |
| `EnableMouse` / `DisableMouse` | SGR 1006 plus the button mode |
| `EnableBracketedPaste` / `DisableBracketedPaste` | Mode 2004 |
| `EnableFocusReporting` / `DisableFocusReporting` | Mode 1004 |
| `DisableAutowrap` / `EnableAutowrap` | Mode 7 |
| `SoftReset` | `CSI !p` |

`CursorStyle` is `Default`, `BlinkingBlock`, `SteadyBlock`, `BlinkingUnderline`, `SteadyUnderline`, `BlinkingBar`, `SteadyBar`.

`Vt.Osc` is `"\x1b]"`. `Vt.St` is the string terminator `"\x1b\\"`. `Vt.Bel` is `'\x07'`.

## Ansi

`Schipper.Io.Xterm.Emit.Ansi` builds the classic sequences: `CursorTo` (1-based row and column), `ClearScreen`, `ClearLine`, `HideCursor`, `ShowCursor`, `EnableAlternateScreen`, `DisableAlternateScreen`, `ResetGraphics`, and `SetGraphics` for a `Cell`. `ScreenRenderer` calls these. Use them when you are writing a single sequence around a `Vt` frame.

`XtermSession` already emits alternate-screen, cursor, autowrap, and the option flags. Emitting them again inside the frame loop fights the session's restore sequence.
