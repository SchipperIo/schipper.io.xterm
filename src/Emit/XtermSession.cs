using System.Text;
using Schipper.Io.Xterm.Transport;

namespace Schipper.Io.Xterm.Emit;

/// <summary>
/// A "raw screen" scope over an <see cref="ITerminal"/>. On <see cref="BeginAsync"/> it writes the
/// enter sequence (alt-screen, hidden cursor, autowrap off, and any requested mouse/paste/focus
/// modes) and on <see cref="DisposeAsync"/> it writes the exact inverse to leave the terminal as it
/// was found.
///
/// <para>Usage: <c>await using var session = await XtermSession.BeginAsync(terminal, options);</c></para>
/// </summary>
public sealed class XtermSession : IAsyncDisposable
{
    private readonly ITerminal _terminal;
    private readonly XtermSessionOptions _options;
    private bool _disposed;

    private XtermSession(ITerminal terminal, XtermSessionOptions options)
    {
        _terminal = terminal;
        _options = options;
    }

    /// <summary>The options this session was started with.</summary>
    public XtermSessionOptions Options => _options;

    /// <summary>Enters a raw-screen session, writing the configured enter sequence to the terminal.</summary>
    public static async ValueTask<XtermSession> BeginAsync(
        ITerminal terminal,
        XtermSessionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        var session = new XtermSession(terminal, options ?? new XtermSessionOptions());
        await terminal.WriteAsync(session.BuildEnter(), cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <summary>The bytes written on enter — exposed for testing and for callers batching their own output.</summary>
    public byte[] BuildEnter()
    {
        var sb = new StringBuilder();
        if (_options.UseAlternateScreen) Ansi.EnableAlternateScreen(sb);
        if (_options.HideCursor) Ansi.HideCursor(sb);
        if (_options.DisableAutowrap) Vt.DisableAutowrap(sb);
        if (_options.MouseMode != MouseMode.Off) Vt.EnableMouse(sb, _options.MouseMode);
        if (_options.BracketedPaste) Vt.EnableBracketedPaste(sb);
        if (_options.FocusReporting) Vt.EnableFocusReporting(sb);
        if (_options.ClearOnEnter)
        {
            Ansi.ResetGraphics(sb);
            Ansi.ClearScreen(sb);
            Ansi.CursorTo(sb, 1, 1);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>The bytes written on leave — the exact inverse of <see cref="BuildEnter"/>, in reverse order.</summary>
    public byte[] BuildLeave()
    {
        var sb = new StringBuilder();
        Ansi.ResetGraphics(sb);
        if (_options.FocusReporting) Vt.DisableFocusReporting(sb);
        if (_options.BracketedPaste) Vt.DisableBracketedPaste(sb);
        if (_options.MouseMode != MouseMode.Off) Vt.DisableMouse(sb);
        if (_options.DisableAutowrap) Vt.EnableAutowrap(sb);
        if (_options.HideCursor) Ansi.ShowCursor(sb);
        if (_options.UseAlternateScreen) Ansi.DisableAlternateScreen(sb);
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Writes the leave sequence and restores the terminal. Idempotent.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _terminal.WriteAsync(BuildLeave()).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            _disposed = true;
            return;
        }

        _disposed = true;
    }
}
