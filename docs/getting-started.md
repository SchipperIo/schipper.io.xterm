# Getting started

[Index](README.md)

`ConsoleTerminal` is an `ITerminal` for the process console. `XtermSession` turns on the alternate screen and the input modes you ask for, and restores them on dispose.

```csharp
using Schipper.Io.Xterm.Emit;
using Schipper.Io.Xterm.Input;
using Schipper.Io.Xterm.Layout;
using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;
using Schipper.Io.Xterm.Transport;

using var terminal = new ConsoleTerminal();
await using var session = await XtermSession.BeginAsync(terminal, new XtermSessionOptions
{
    MouseMode = MouseMode.ButtonEvent,
    BracketedPaste = true,
    FocusReporting = true,
});

var layout = new ScreenLayout(terminal.Columns, terminal.Rows);
Region header = layout.AddTop(1, name: "header");
Region footer = layout.AddBottom(2, BorderStyle.Single, BorderSides.Top, name: "footer");
Region body = layout.AddFill(name: "body");

var screen = new ScreenBuffer(terminal.Columns, terminal.Rows);
ScreenBuffer? previous = null;
var input = new XtermInput();
var bytes = new byte[4096];

terminal.Resized += () =>
{
    layout.Resize(terminal.Columns, terminal.Rows);
    screen.Resize(terminal.Columns, terminal.Rows);
    previous = null;
};

while (true)
{
    Draw.Header(
        header.Surface,
        new Rect(0, 0, header.ContentWidth, header.ContentHeight),
        " Workshop ",
        Color.Basic(BasicColor.Black),
        Color.Basic(BasicColor.Cyan));

    body.Surface.Clear();
    Draw.Text(
        body.Surface,
        new Rect(1, 1, Math.Max(1, body.ContentWidth - 2), 1),
        "Press q or Esc to quit.",
        Color.Basic(BasicColor.BrightBlack),
        Color.Default);

    Draw.Text(
        footer.Surface,
        new Rect(0, 0, footer.ContentWidth, footer.ContentHeight),
        "ready",
        Color.Default,
        Color.Default);

    layout.Compose(screen);
    string frame = ScreenRenderer.RenderDiff(previous, screen);
    if (frame.Length > 0)
    {
        await terminal.WriteAsync(frame);
        previous = screen.Clone();
    }

    int read = await terminal.ReadAsync(bytes);
    if (read == 0)
    {
        break;
    }

    foreach (TerminalEvent ev in input.Feed(bytes.AsSpan(0, read)))
    {
        if (ev.Kind == TerminalEventKind.Key &&
            (ev.Key.Key == Key.Escape || (ev.Key.IsChar && ev.Key.Char is 'q' or 'Q')))
        {
            return;
        }
    }

    if (read < bytes.Length && input.PendingIsLoneEscape)
    {
        TerminalEvent? flushed = input.Flush();
        if (flushed is { } ev &&
            ev.Kind == TerminalEventKind.Key &&
            ev.Key.Key == Key.Escape)
        {
            return;
        }
    }
}
```

`ConsoleTerminal.Dispose` restores the console mode it changed. `XtermSession.DisposeAsync` writes the inverse of the enter sequence. Dispose the session first, then the terminal. `await using` and `using` in that order do this when the block ends.

Run the program in a real terminal. A redirected stdin has no window size and no raw key stream.

`ITerminal` is the seam for any other byte pipe. Implement `Columns`, `Rows`, `Resized`, `WriteAsync`, and `ReadAsync`. `ReadAsync` returns 0 at end of stream. `TerminalExtensions.WriteAsync(string)` encodes UTF-8. On Windows, `ConsoleTerminal` sets the console to UTF-8 (code page 65001), turns virtual-terminal input on, and clears quick-edit so SGR mouse reports arrive.

`demo/FileBrowser.csproj` is a complete program on this package. Its `Program.cs` turns on bracketed paste so a paste is one event, not typed keys. The run loop idle-flushes a lone ESC.

## Session options

`XtermSessionOptions` defaults turn the alternate screen on, hide the cursor, disable autowrap, and clear on enter. Mouse, bracketed paste, and focus reporting stay off until you set them.

| Property | Default |
| --- | --- |
| `UseAlternateScreen` | true |
| `HideCursor` | true |
| `DisableAutowrap` | true |
| `ClearOnEnter` | true |
| `MouseMode` | `MouseMode.Off` |
| `BracketedPaste` | false |
| `FocusReporting` | false |

`BuildEnter` and `BuildLeave` return the byte sequences. `BeginAsync` writes the enter sequence. `Options` is the instance the session was started with.

See [Input and sequences](input-and-emit.md), [Layout](layout.md), and [Screen and glyphs](screen-and-glyphs.md).
