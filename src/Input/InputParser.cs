using System.Runtime.InteropServices;
using Schipper.Io.Xterm.Emit;

namespace Schipper.Io.Xterm.Input;

/// <summary>
/// Stateful decoder that turns incoming terminal bytes into <see cref="KeyEvent"/>s. Handles control
/// characters, CSI/SS3 escape sequences for arrows/function/navigation keys, and Alt-prefixed keys,
/// buffering partial escape sequences and incomplete UTF-8 that arrive split across reads.
/// </summary>
public sealed class InputParser
{
    private readonly List<byte> _pending = new();

    /// <summary>Number of bytes buffered awaiting a complete sequence.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>True when the only buffered byte is a lone ESC (the Escape key, not a sequence start).</summary>
    public bool PendingIsLoneEscape => _pending.Count == 1 && _pending[0] == Ansi.Escape;

    /// <summary>Feeds raw bytes and returns the key events that could be fully decoded.</summary>
    public IReadOnlyList<KeyEvent> Feed(ReadOnlySpan<byte> bytes)
    {
        var events = new List<KeyEvent>();
        foreach (byte b in bytes)
        {
            _pending.Add(b);
        }

        while (_pending.Count > 0)
        {
            Span<byte> span = CollectionsMarshal.AsSpan(_pending);
            if (!TryParse(span, out int consumed, out KeyEvent ev, out bool incomplete))
            {
                _ = incomplete;
                break;
            }

            _pending.RemoveRange(0, consumed);
            if (ev.Key != Key.None)
            {
                events.Add(ev);
            }
        }

        return events;
    }

    /// <summary>
    /// Resolves a buffered lone ESC as Escape, or a held CR as Enter. A longer incomplete CSI or
    /// paste introducer stays buffered. Call this when input has gone idle.
    /// </summary>
    public KeyEvent? Flush()
    {
        if (_pending.Count == 0)
        {
            return null;
        }

        if (_pending.Count == 1 && _pending[0] == Ansi.Escape)
        {
            _pending.Clear();
            return new KeyEvent(Key.Escape);
        }

        if (_pending[0] == Ansi.Escape)
        {
            return null;
        }

        if (_pending[0] == 0x0D)
        {
            _pending.RemoveAt(0);
            return new KeyEvent(Key.Enter);
        }

        if (IsIncompleteUtf8Lead(_pending[0]))
        {
            _pending.Clear();
            return KeyEvent.FromChar('\uFFFD');
        }

        return null;
    }

    private static bool TryParse(ReadOnlySpan<byte> buf, out int consumed, out KeyEvent ev, out bool incomplete)
    {
        consumed = 0;
        ev = default;
        incomplete = false;

        if (buf.Length == 0)
        {
            return false;
        }

        byte b = buf[0];
        if (b != Ansi.Escape)
        {
            return TryParseNonEscape(buf, out consumed, out ev, out incomplete);
        }

        if (buf.Length == 1)
        {
            incomplete = true;
            return false;
        }

        byte b1 = buf[1];
        if (b1 == 'O')
        {
            if (buf.Length < 3)
            {
                incomplete = true;
                return false;
            }

            consumed = 3;
            ev = MapSs3((char)buf[2]);
            return true;
        }

        if (b1 == '[')
        {
            for (int i = 2; i < buf.Length; i++)
            {
                byte c = buf[i];
                if (c is >= 0x40 and <= 0x7E)
                {
                    consumed = i + 1;
                    ev = MapCsi(buf.Slice(2, i - 2), (char)c);
                    return true;
                }

                if (c < 0x20)
                {
                    consumed = i;
                    ev = new KeyEvent(Key.None);
                    return true;
                }
            }

            incomplete = true;
            return false;
        }

        if (b1 is >= 0x20 and < 0x7F)
        {
            consumed = 2;
            ev = KeyEvent.FromChar((char)b1, alt: true);
            return true;
        }

        if (b1 >= 0x80)
        {
            if (!TryReadUtf8(buf[1..], out int utfConsumed, out int scalar, out incomplete))
            {
                return false;
            }

            consumed = 1 + utfConsumed;
            ev = KeyEvent.FromChar(ToBmpChar(scalar), alt: true);
            return true;
        }

        consumed = 1;
        ev = new KeyEvent(Key.Escape);
        return true;
    }

    private static bool TryParseNonEscape(ReadOnlySpan<byte> buf, out int consumed, out KeyEvent ev, out bool incomplete)
    {
        incomplete = false;
        byte b = buf[0];

        if (b == 0x0D)
        {
            if (buf.Length == 1)
            {
                consumed = 0;
                ev = default;
                incomplete = true;
                return false;
            }

            consumed = buf[1] == 0x0A ? 2 : 1;
            ev = new KeyEvent(Key.Enter);
            return true;
        }

        if (b >= 0x80)
        {
            if (!TryReadUtf8(buf, out consumed, out int scalar, out incomplete))
            {
                ev = default;
                return false;
            }

            ev = KeyEvent.FromChar(ToBmpChar(scalar));
            return true;
        }

        consumed = 1;
        ev = DecodeSingle(b);
        return true;
    }

    private static KeyEvent DecodeSingle(byte b) => b switch
    {
        0x0D or 0x0A => new KeyEvent(Key.Enter),
        0x09 => new KeyEvent(Key.Tab),
        0x7F or 0x08 => new KeyEvent(Key.Backspace),
        0x00 => new KeyEvent(Key.Char, ' ', ctrl: true),
        0x1C => new KeyEvent(Key.Char, '\\', ctrl: true),
        0x1D => new KeyEvent(Key.Char, ']', ctrl: true),
        0x1E => new KeyEvent(Key.Char, '^', ctrl: true),
        0x1F => new KeyEvent(Key.Char, '_', ctrl: true),
        >= 1 and <= 26 => new KeyEvent(Key.Char, (char)('a' + (b - 1)), ctrl: true),
        < 0x20 => new KeyEvent(Key.None),
        _ => KeyEvent.FromChar((char)b),
    };

    private static bool TryReadUtf8(ReadOnlySpan<byte> buf, out int consumed, out int scalar, out bool incomplete)
    {
        consumed = 0;
        scalar = 0xFFFD;
        incomplete = false;

        byte lead = buf[0];
        int needed;
        int min;
        int value;
        if ((lead & 0xE0) == 0xC0)
        {
            needed = 2;
            value = lead & 0x1F;
            min = 0x80;
        }
        else if ((lead & 0xF0) == 0xE0)
        {
            needed = 3;
            value = lead & 0x0F;
            min = 0x800;
        }
        else if ((lead & 0xF8) == 0xF0)
        {
            needed = 4;
            value = lead & 0x07;
            min = 0x10000;
        }
        else
        {
            consumed = 1;
            return true;
        }

        if (buf.Length < needed)
        {
            incomplete = true;
            return false;
        }

        for (int i = 1; i < needed; i++)
        {
            byte c = buf[i];
            if ((c & 0xC0) != 0x80)
            {
                consumed = 1;
                return true;
            }

            value = (value << 6) | (c & 0x3F);
        }

        consumed = needed;
        if (value < min || value > 0x10FFFF || value is >= 0xD800 and <= 0xDFFF)
        {
            scalar = 0xFFFD;
            return true;
        }

        scalar = value;
        return true;
    }

    private static char ToBmpChar(int scalar) =>
        scalar is > 0 and <= char.MaxValue ? (char)scalar : '\uFFFD';

    private static bool IsIncompleteUtf8Lead(byte b) =>
        (b & 0xE0) == 0xC0 || (b & 0xF0) == 0xE0 || (b & 0xF8) == 0xF0;

    private static KeyEvent MapSs3(char final) => final switch
    {
        'A' => new KeyEvent(Key.Up),
        'B' => new KeyEvent(Key.Down),
        'C' => new KeyEvent(Key.Right),
        'D' => new KeyEvent(Key.Left),
        'H' => new KeyEvent(Key.Home),
        'F' => new KeyEvent(Key.End),
        'P' => new KeyEvent(Key.F1),
        'Q' => new KeyEvent(Key.F2),
        'R' => new KeyEvent(Key.F3),
        'S' => new KeyEvent(Key.F4),
        _ => new KeyEvent(Key.None),
    };

    private static KeyEvent MapCsi(ReadOnlySpan<byte> parameters, char final)
    {
        switch (final)
        {
            case 'A': return new KeyEvent(Key.Up);
            case 'B': return new KeyEvent(Key.Down);
            case 'C': return new KeyEvent(Key.Right);
            case 'D': return new KeyEvent(Key.Left);
            case 'H': return new KeyEvent(Key.Home);
            case 'F': return new KeyEvent(Key.End);
            case 'P': return new KeyEvent(Key.F1);
            case 'Q': return new KeyEvent(Key.F2);
            case 'R': return new KeyEvent(Key.F3);
            case 'S': return new KeyEvent(Key.F4);
            case '~':
                return ParseLeadingInt(parameters) switch
                {
                    1 or 7 => new KeyEvent(Key.Home),
                    2 => new KeyEvent(Key.Insert),
                    3 => new KeyEvent(Key.Delete),
                    4 or 8 => new KeyEvent(Key.End),
                    5 => new KeyEvent(Key.PageUp),
                    6 => new KeyEvent(Key.PageDown),
                    11 => new KeyEvent(Key.F1),
                    12 => new KeyEvent(Key.F2),
                    13 => new KeyEvent(Key.F3),
                    14 => new KeyEvent(Key.F4),
                    15 => new KeyEvent(Key.F5),
                    17 => new KeyEvent(Key.F6),
                    18 => new KeyEvent(Key.F7),
                    19 => new KeyEvent(Key.F8),
                    20 => new KeyEvent(Key.F9),
                    21 => new KeyEvent(Key.F10),
                    23 => new KeyEvent(Key.F11),
                    24 => new KeyEvent(Key.F12),
                    _ => new KeyEvent(Key.None),
                };
            default:
                return new KeyEvent(Key.None);
        }
    }

    private static int ParseLeadingInt(ReadOnlySpan<byte> parameters)
    {
        int value = 0;
        bool any = false;
        foreach (byte c in parameters)
        {
            if (c is >= (byte)'0' and <= (byte)'9')
            {
                value = (value * 10) + (c - '0');
                any = true;
            }
            else
            {
                break;
            }
        }

        return any ? value : -1;
    }
}
