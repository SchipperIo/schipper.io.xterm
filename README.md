# Schipper.Io.Xterm

A zero-dependency, modern terminal toolkit for building rich full-screen TUIs in .NET. One package,
no dependencies — it bundles everything it needs (the cell grid, diffing renderer, color model,
input parser, and the `ITerminal` transport abstraction) and adds the modern terminal layer on top:

- **Emit** modern xterm sequences: OSC window title / hyperlinks (OSC 8) / clipboard (OSC 52),
  cursor styles (DECSCUSR), synchronized output (mode 2026), and enabling mouse tracking
  (SGR 1006), bracketed paste (2004) and focus reporting (1004). `XtermSession` is an
  `IAsyncDisposable` "raw screen" that flips alt-screen + cursor + the chosen features on and
  restores them on dispose.
- **Decode** rich input into `TerminalEvent`s: SGR mouse (click/drag/wheel), bracketed-paste
  blocks, focus in/out, and modifier-aware keys (CSI-u / modified CSI/SS3) — with plain keys
  decoded by the bundled `InputParser`.
- **Lay out** screens two ways: immediate-mode painters (`Draw` over a `ScreenBuffer` you own:
  borders, header/footer, modal, splash) and a retained `ScreenLayout` whose regions report
  their solved size and can negotiate it (`Region.RequestHeight`, configurable borders), with a
  modal/overlay stack and a fullscreen mode.
- **Run on the local console** out of the box: `ConsoleTerminal` is a batteries-included
  `ITerminal` that puts a real console into raw/VT mode (Windows `SetConsoleMode`, Unix `stty`)
  so the rich input stream actually reaches `XtermInput`. See [sample.md](sample.md) for a
  complete standalone console-app example.

Everything is AOT-compatible and BCL-only.

## Documentation

Usage of the public API is in [docs/](docs/README.md). The file browser in [demo/](demo/README.md) is a complete program on this package.

## Build, test, package

Builds run in the latest .NET 10 SDK container, `mcr.microsoft.com/dotnet/sdk:10.0`. Docker is required. `build.ps1` and `build.sh` both run `container.sh` inside that image. The source is copied into `/tmp` inside the container, so `bin/` and `obj/` stay off the host. A packed package is written to `./dist` and copied to the shared local feed at `../nuget.cache`.

`nuget.config` restores `Schipper.*` from that feed and every other package from nuget.org. `global.json` requests SDK 10.0.100 and rolls forward to the latest .NET 10 SDK in the image.

No flags builds Release. Flags combine. The runtime identifier defaults to `linux-x64` (`RID=win-x64 ./build.sh` or `./build.ps1 -Rid win-x64`).

```bash
./build.sh           # restore + build
./build.sh -t        # unit tests
./build.sh -i        # integration tests, if any
./build.sh -p        # pack into ./dist and ../nuget.cache
./build.sh -r        # run, when the project is an executable
./build.sh -q        # unit tests under dotnet-trace -> ./dist/trace
./build.sh -o        # also write build/test logs to ./dist/raw
./build.sh -t -p     # flags combine
```

```powershell
./build.ps1
./build.ps1 -t -p
```

## Continuous integration

`.github/workflows/build.yml` runs `./build.sh -t -p` on Ubuntu for every push and pull request, using the same SDK container. The packed package is uploaded as the `nuget` workflow artifact.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
