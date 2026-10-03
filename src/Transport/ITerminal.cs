namespace Schipper.Io.Xterm.Transport;

/// <summary>
/// A connected, full-duplex terminal: a byte channel plus the current cell dimensions. The wire is
/// UTF-8. <see cref="Resized"/> fires when the window size changes. <see cref="ConsoleTerminal"/> is
/// the built-in local-console implementation; provide your own to drive any other transport.
/// </summary>
public interface ITerminal
{
    int Columns { get; }

    int Rows { get; }

    /// <summary>Raised when the window size changes.</summary>
    event Action? Resized;

    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);

    /// <summary>Reads available bytes into <paramref name="buffer"/>; returns 0 at end of stream.</summary>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
