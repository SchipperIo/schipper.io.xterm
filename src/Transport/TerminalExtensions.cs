using System.Text;

namespace Schipper.Io.Xterm.Transport;

/// <summary>Convenience helpers for writing UTF-8 text/ANSI to an <see cref="ITerminal"/>.</summary>
public static class TerminalExtensions
{
    /// <summary>Encodes a string as UTF-8. ANSI escape bytes are ASCII and pass through unchanged.</summary>
    public static byte[] Encode(this ITerminal terminal, string text) => Encoding.UTF8.GetBytes(text);

    public static ValueTask WriteAsync(this ITerminal terminal, string text, CancellationToken cancellationToken = default) =>
        terminal.WriteAsync(terminal.Encode(text), cancellationToken);
}
