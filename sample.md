# Sample: a standalone .NET console app on `Schipper.Io.Xterm`

One package, nothing else. `ConsoleTerminal` is the transport; `XtermSession` flips the screen into
raw/alt mode; `ScreenLayout` gives you header/body/footer; `XtermInput` decodes keys + mouse +
paste + focus. Press **q** or **Esc** to quit; click the mouse and watch the footer update.

## `MyTui.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- Optional: single native binary, no runtime needed -->
    <PublishAot>true</PublishAot>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Schipper.Io.Xterm" Version="0.1.0-dev" />
  </ItemGroup>

</Project>
```

> For local development against the source tree you can swap the `PackageReference` for a
> `<ProjectReference>` to `src/Schipper.Io.Xterm.csproj`.

## `Program.cs`

```csharp
using Schipper.Io.Xterm;

// 1) Transport: the local console. Raw/VT mode on; restored on dispose.
using var terminal = new ConsoleTerminal();

// 2) Screen scope: alt-screen + hidden cursor + mouse + bracketed paste + focus reporting.
await using var session = await XtermSession.BeginAsync(terminal, new XtermSessionOptions
{
    MouseMode = MouseMode.ButtonEvent,
    BracketedPaste = true,
    FocusReporting = true,
});

// 3) Layout: a 1-row header, a 1-row footer (with a top border), and a fill body in between.
var layout = new ScreenLayout(terminal.Columns, terminal.Rows);
Region header = layout.AddTop(1, name: "header");
Region footer = layout.AddBottom(2, BorderStyle.Single, BorderSides.Top, name: "footer");
Region body = layout.AddFill(name: "body");

var screen = new ScreenBuffer(terminal.Columns, terminal.Rows);
string status = "ready — click the mouse, type, or paste";
bool focused = true;
bool dirty = true;

// Repaint when the window is resized.
terminal.Resized += () =>
{
    layout.Resize(terminal.Columns, terminal.Rows);
    screen.Resize(terminal.Columns, terminal.Rows);
    dirty = true;
};

void Paint()
{
    // Draw into each region's own surface…
    Draw.Header(header.Surface, new Rect(0, 0, header.ContentWidth, header.ContentHeight),
        " Schipper.Io.Xterm demo ", Color.Black, Color.Basic(BasicColor.Cyan));

    body.Surface.Clear();
    Draw.Text(body.Surface, new Rect(1, 1, body.ContentWidth - 2, 1),
        focused ? "Window focused." : "Window blurred.", Color.Default, Color.Default);
    Draw.Text(body.Surface, new Rect(1, 3, body.ContentWidth - 2, 1),
        "Press q or Esc to quit.", Color.Basic(BasicColor.BrightBlack), Color.Default);

    Draw.Text(footer.Surface, new Rect(0, 0, footer.ContentWidth, 1),
        status, Color.Default, Color.Default);

    // …then compose them into the screen buffer and flush.
    screen.Clear();
    layout.Compose(screen);
    return; // (Paint is sync; the flush happens below)
}

var input = new XtermInput();
var buffer = new byte[1024];
ScreenBuffer? previous = null;
bool running = true;

while (running)
{
    if (dirty)
    {
        Paint();
        // Minimal diff against the previous frame; first frame paints fully.
        string frame = ScreenRenderer.RenderDiff(previous, screen);
        if (frame.Length > 0)
        {
            await terminal.WriteAsync(frame);
        }

        previous = screen.Clone();
        dirty = false;
    }

    int n = await terminal.ReadAsync(buffer);
    if (n == 0)
    {
        break; // stdin closed
    }

    foreach (TerminalEvent ev in input.Feed(buffer.AsSpan(0, n)))
    {
        switch (ev.Kind)
        {
            case TerminalEventKind.Key:
                if (ev.Key.Key == Key.Escape ||
                    (ev.Key.IsChar && char.ToLowerInvariant(ev.Key.Char) == 'q'))
                {
                    running = false;
                }
                else if (ev.Key.IsChar)
                {
                    status = $"key: '{ev.Key.Char}'" +
                             (ev.Key.Ctrl ? " +ctrl" : "") + (ev.Key.Alt ? " +alt" : "");
                }
                else
                {
                    status = $"key: {ev.Key.Key}";
                }

                break;

            case TerminalEventKind.Mouse:
                MouseEvent m = ev.Mouse;
                status = $"mouse: {m.Action} {m.Button} @ ({m.X},{m.Y})";
                break;

            case TerminalEventKind.Paste:
                status = $"pasted {ev.PasteText.Length} chars";
                break;

            case TerminalEventKind.Focus:
                focused = ev.Focused;
                break;
        }

        dirty = true;
    }
}
// `session` dispose restores the screen; `terminal` dispose restores console mode.
```

## Run it

```bash
dotnet run                 # framework-dependent
dotnet publish -r linux-x64 -c Release   # single native binary (PublishAot)
```

That's the whole thing: ~80 lines of app code for a focus-aware, mouse-driven, paste-aware
full-screen TUI with a bordered footer — and the only dependency is `Schipper.Io.Xterm`.
