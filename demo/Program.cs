using Schipper.Io.Xterm.Emit;
using Schipper.Io.Xterm.Transport;

namespace Schipper.Io.FileBrowser;

internal static class Program
{
    private static async Task Main()
    {
        // ConsoleTerminal puts the real console into raw/VT mode; XtermSession switches to the
        // alternate screen, hides the cursor and turns on mouse reporting. Both restore on dispose.
        using var terminal = new ConsoleTerminal();
        await using var session = await XtermSession.BeginAsync(terminal, new XtermSessionOptions
        {
            MouseMode = MouseMode.ButtonEvent,
            BracketedPaste = true,
        });

        var app = new BrowserApp(terminal);
        await app.RunAsync();
    }
}
