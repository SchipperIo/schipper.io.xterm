# FileBrowser — a Schipper.Io.Xterm demo

A read-only, two-pane file/directory browser in ~450 lines, built entirely on `Schipper.Io.Xterm`
with no other dependencies.

```
┌ Folders ───────────┐┌ Contents ───────────────────────────────────┐
│ ↑ ..               ││ Name                              Created    │
│ ▸ docs             ││ ▸ docs                       2026-06-24 21:10│
│ ▸ src              ││ ▸ src                        2026-06-24 21:27│
│ ▸ tests            ││ · README.md                  2026-06-24 22:45│
│                    ││ · Schipper.Io.Xterm.csproj      2026-06-24 23:02│
│                    ││ · build.sh                   2026-06-24 22:59│
└────────────────────┘└──────────────────────────────────────────────┘
 ↑↓ move   ⏎/→ open   ←/⌫ up   Tab switch pane   q quit   3 folders · 2 files
```

## Run

```bash
dotnet run --project demo/FileBrowser.csproj
```

(Use a real terminal — Windows Terminal, or any Unix terminal — not a redirected/piped console.)

## Controls

| Key | Action |
| --- | --- |
| `↑` / `↓` | Move the selection in the focused pane |
| `PgUp` / `PgDn` | Page the selection |
| `Home` / `End` | Jump to first / last |
| `Enter` / `→` | Open the selected folder (or drive) |
| `←` / `Backspace` | Go up to the parent (drives at the top) |
| `Tab` | Switch focus between the Folders and Contents panes |
| `q` / `Esc` | Quit |
| Mouse | Click a row to select it (and focus its pane); wheel scrolls |

Files are colored by extension: source = cyan, config = yellow, images = magenta,
archives = red, executables = green, media = magenta, documents = bright red; folders
are bright blue.

## What it shows

- **`ConsoleTerminal`** — the local-console transport (raw/VT mode in, ANSI out), so it runs as a
  plain console app with no extra packages.
- **`XtermSession`** — alternate screen + hidden cursor + SGR mouse reporting, restored on exit.
- **`ScreenLayout`** — a 1-row header, a 1-row footer, and a fill body region.
- **`Draw` + `Rect`** — the body is split into two bordered panes; rows are painted with
  per-cell colors and a selection highlight.
- **`XtermInput`** — keyboard, mouse clicks, and wheel decoded into `TerminalEvent`s.
- **`ScreenRenderer.RenderDiff`** — only the cells that changed are written each frame.

It is strictly read-only: it never opens, modifies, moves, or deletes anything — it only
enumerates directory listings and their creation times.

## Files

- `Program.cs` — bootstrap: `ConsoleTerminal` + `XtermSession`, then run the app.
- `BrowserApp.cs` — layout, navigation state, input handling, and rendering.
- `Entry.cs` — the row model and the extension→color map.
