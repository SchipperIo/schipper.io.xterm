namespace Schipper.Io.Xterm.Transport;

/// <summary>
/// A glyph-encoding hint a transport may carry in its handshake. The wire itself is UTF-8; CP437
/// survives only as an on-disk art format via <see cref="Cp437"/>, never as a transport encoding.
/// </summary>
public enum TerminalEncoding : byte
{
    /// <summary>Classic IBM PC code page 437.</summary>
    Cp437 = 0,

    /// <summary>UTF-8.</summary>
    Utf8 = 1,
}
