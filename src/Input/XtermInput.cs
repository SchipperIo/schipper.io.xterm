using System.Text;

namespace Schipper.Io.Xterm.Input;

/// <summary>
/// Rich input decoder that turns incoming terminal bytes into <see cref="TerminalEvent"/>s: SGR
/// (1006) mouse reports, bracketed-paste blocks, focus in/out, and modifier-aware keys (CSI-u and
/// modified CSI/SS3). Anything that is not one of those modern sequences — plain characters, control
/// codes, and ordinary arrow/function keys — is delegated to a wrapped <see cref="InputParser"/> so
/// that key-decoding logic lives in exactly one place. Partial sequences split across reads are
/// buffered, exactly like <see cref="InputParser"/>.
/// </summary>
public sealed class XtermInput
{
    private const byte Esc = 0x1B;

    private readonly InputParser _keys = new();
    private readonly List<byte> _pending = new();

    /// <summary>Number of bytes buffered awaiting a complete sequence.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>True when the only buffered byte is a lone ESC (the Escape key, not a sequence start).</summary>
    public bool PendingIsLoneEscape => _pending.Count == 1 && _pending[0] == Esc;

    /// <summary>Feeds raw bytes and returns the terminal events that could be fully decoded.</summary>
    public IReadOnlyList<TerminalEvent> Feed(ReadOnlySpan<byte> bytes)
    {
        var events = new List<TerminalEvent>();
        foreach (byte b in bytes)
        {
            _pending.Add(b);
        }

        int cursor = 0;
        while (cursor < _pending.Count)
        {
            byte b0 = _pending[cursor];

            if (b0 != Esc)
            {
                // A run of ordinary bytes up to the next ESC — hand it to the key parser whole.
                int runEnd = cursor;
                while (runEnd < _pending.Count && _pending[runEnd] != Esc)
                {
                    runEnd++;
                }

                DelegateKeys(events, cursor, runEnd - cursor);
                cursor = runEnd;
                continue;
            }

            if (!TryConsumeEscape(events, cursor, out int consumed))
            {
                break; // incomplete sequence; wait for more bytes.
            }

            cursor += consumed;
        }

        if (cursor > 0)
        {
            _pending.RemoveRange(0, cursor);
        }

        return events;
    }

    /// <summary>
    /// Resolves a buffered lone ESC as an Escape press. A longer incomplete CSI, mouse report, or
    /// paste stays buffered. When this decoder has nothing pending, forwards to the key parser so a
    /// held CR or incomplete UTF-8 is not stranded. Call when input has gone idle.
    /// </summary>
    public TerminalEvent? Flush()
    {
        if (_pending.Count == 1 && _pending[0] == Esc)
        {
            _pending.Clear();
            return TerminalEvent.FromKey(new KeyEvent(Key.Escape));
        }

        if (_pending.Count > 0)
        {
            return null;
        }

        KeyEvent? key = _keys.Flush();
        return key is { } k ? TerminalEvent.FromKey(k) : null;
    }

    // Tries to consume one ESC-introduced unit starting at <paramref name="start"/>. Returns false
    // (without consuming) when the sequence is not yet complete.
    private bool TryConsumeEscape(List<TerminalEvent> events, int start, out int consumed)
    {
        consumed = 0;
        int len = _pending.Count - start;
        if (len < 2)
        {
            return false; // lone ESC so far — could be Escape key or a sequence start; wait.
        }

        byte b1 = _pending[start + 1];

        // CSI: ESC [ ...
        if (b1 == (byte)'[')
        {
            return TryConsumeCsi(events, start, out consumed);
        }

        // SS3: ESC O x  (ordinary arrow/function keys) — let the key parser handle it.
        if (b1 == (byte)'O')
        {
            if (len < 3)
            {
                return false;
            }

            DelegateKeys(events, start, 3);
            consumed = 3;
            return true;
        }

        // ESC + printable = Alt+<char> — the key parser knows this form.
        if (b1 is >= 0x20 and < 0x7F)
        {
            DelegateKeys(events, start, 2);
            consumed = 2;
            return true;
        }

        if (b1 >= 0x80)
        {
            int utfLen = Utf8SequenceLength(b1);
            if (utfLen < 0)
            {
                events.Add(TerminalEvent.FromKey(new KeyEvent(Key.Escape)));
                consumed = 1;
                return true;
            }

            if (len < 1 + utfLen)
            {
                return false;
            }

            DelegateKeys(events, start, 1 + utfLen);
            consumed = 1 + utfLen;
            return true;
        }

        // ESC + control byte: emit Escape now, leave the control byte for the next pass.
        events.Add(TerminalEvent.FromKey(new KeyEvent(Key.Escape)));
        consumed = 1;
        return true;
    }

    private bool TryConsumeCsi(List<TerminalEvent> events, int start, out int consumed)
    {
        consumed = 0;

        // Layout: ESC [ [marker] params final. marker is one of < = > ? ; final is 0x40..0x7E.
        int i = start + 2;
        byte marker = 0;
        if (i < _pending.Count && _pending[i] is (byte)'<' or (byte)'=' or (byte)'>' or (byte)'?')
        {
            marker = _pending[i];
            i++;
        }

        int paramStart = i;
        while (i < _pending.Count)
        {
            byte c = _pending[i];
            if (c is >= 0x40 and <= 0x7E)
            {
                break;
            }

            if (c < 0x20)
            {
                // ESC or another C0: drop the broken introducer and reprocess from this byte.
                consumed = i - start;
                return true;
            }

            i++;
        }

        if (i >= _pending.Count)
        {
            return false; // no final byte yet.
        }

        char final = (char)_pending[i];
        int paramLen = i - paramStart;
        ReadOnlySpan<byte> paramBytes = CopyRange(paramStart, paramLen);
        int tokenLen = (i - start) + 1;

        // SGR mouse: ESC [ < b ; x ; y M|m
        if (marker == (byte)'<' && (final is 'M' or 'm'))
        {
            events.Add(DecodeMouse(paramBytes, final));
            consumed = tokenLen;
            return true;
        }

        if (marker == 0)
        {
            int leading = LeadingParam(paramBytes);

            // Bracketed paste start: ESC [ 200 ~  — consume through the matching end marker.
            if (final == '~' && leading == 200)
            {
                return TryConsumePaste(events, start, tokenLen, out consumed);
            }

            // Focus in / out (no params).
            if (paramLen == 0 && final == 'I')
            {
                events.Add(TerminalEvent.FromFocus(true));
                consumed = tokenLen;
                return true;
            }

            if (paramLen == 0 && final == 'O')
            {
                events.Add(TerminalEvent.FromFocus(false));
                consumed = tokenLen;
                return true;
            }

            // CSI-u extended key: ESC [ code ; mods u
            if (final == 'u')
            {
                events.Add(DecodeCsiU(paramBytes));
                consumed = tokenLen;
                return true;
            }

            // Modified key: ESC [ 1 ; mod {A B C D H F P Q R S}  or  ESC [ n ; mod ~
            if (TryDecodeModifiedKey(paramBytes, final, out KeyEvent modified))
            {
                events.Add(TerminalEvent.FromKey(modified));
                consumed = tokenLen;
                return true;
            }
        }

        // Anything else (plain arrows/function keys, unknown CSIs) goes to the key parser verbatim.
        DelegateKeys(events, start, tokenLen);
        consumed = tokenLen;
        return true;
    }

    private bool TryConsumePaste(List<TerminalEvent> events, int start, int startTokenLen, out int consumed)
    {
        consumed = 0;
        ReadOnlySpan<byte> end = "\x1b[201~"u8;
        int textStart = start + startTokenLen;

        for (int p = textStart; p + end.Length <= _pending.Count; p++)
        {
            if (MatchesAt(p, end))
            {
                int textLen = p - textStart;
                byte[] text = CopyRange(textStart, textLen);
                events.Add(TerminalEvent.FromPaste(Encoding.UTF8.GetString(text)));
                consumed = (p + end.Length) - start;
                return true;
            }
        }

        return false; // end marker not seen yet — wait for more bytes.
    }

    private void DelegateKeys(List<TerminalEvent> events, int start, int length)
    {
        if (length <= 0)
        {
            return;
        }

        byte[] slice = CopyRange(start, length);
        foreach (KeyEvent key in _keys.Feed(slice))
        {
            events.Add(TerminalEvent.FromKey(key));
        }
    }

    private static TerminalEvent DecodeMouse(ReadOnlySpan<byte> paramBytes, char final)
    {
        Span<int> v = stackalloc int[3];
        int count = SplitParams(paramBytes, v);
        int b = count > 0 ? v[0] : 0;
        int px = count > 1 ? v[1] : 1;
        int py = count > 2 ? v[2] : 1;

        bool shift = (b & 4) != 0;
        bool alt = (b & 8) != 0;
        bool ctrl = (b & 16) != 0;
        bool motion = (b & 32) != 0;
        bool wheel = (b & 64) != 0;
        bool extended = (b & 128) != 0;
        int low = b & 3;

        MouseButton button;
        MouseAction action;
        if (wheel)
        {
            button = low switch { 0 => MouseButton.WheelUp, 1 => MouseButton.WheelDown, 2 => MouseButton.WheelLeft, _ => MouseButton.WheelRight };
            action = MouseAction.Press;
        }
        else if (extended)
        {
            button = MouseButton.None;
            action = motion ? MouseAction.Move : (final == 'M' ? MouseAction.Press : MouseAction.Release);
        }
        else
        {
            button = low switch { 0 => MouseButton.Left, 1 => MouseButton.Middle, 2 => MouseButton.Right, _ => MouseButton.None };
            action = motion ? MouseAction.Move : (final == 'M' ? MouseAction.Press : MouseAction.Release);
        }

        int x = Math.Max(0, px - 1);
        int y = Math.Max(0, py - 1);
        return TerminalEvent.FromMouse(new MouseEvent(button, action, x, y, ctrl, alt, shift));
    }

    private static TerminalEvent DecodeCsiU(ReadOnlySpan<byte> paramBytes)
    {
        Span<int> v = stackalloc int[3];
        int count = SplitParams(paramBytes, v);
        int code = count > 0 ? v[0] : 0;
        int mod = count > 1 ? v[1] : 1;
        DecodeModifiers(mod, out bool ctrl, out bool alt, out bool shift);

        KeyEvent ev = code switch
        {
            13 or 10 => new KeyEvent(Key.Enter, ctrl: ctrl, alt: alt, shift: shift),
            9 => new KeyEvent(Key.Tab, ctrl: ctrl, alt: alt, shift: shift),
            27 => new KeyEvent(Key.Escape, ctrl: ctrl, alt: alt, shift: shift),
            127 or 8 => new KeyEvent(Key.Backspace, ctrl: ctrl, alt: alt, shift: shift),
            > 0 and <= 0x10FFFF when code <= char.MaxValue => new KeyEvent(Key.Char, (char)code, ctrl, alt, shift),
            _ => new KeyEvent(Key.None),
        };

        return TerminalEvent.FromKey(ev);
    }

    private static bool TryDecodeModifiedKey(ReadOnlySpan<byte> paramBytes, char final, out KeyEvent ev)
    {
        ev = default;
        Span<int> v = stackalloc int[3];
        int count = SplitParams(paramBytes, v);
        if (count < 2)
        {
            return false; // unmodified — let the key parser handle it.
        }

        DecodeModifiers(v[1], out bool ctrl, out bool alt, out bool shift);

        if (final == '~' && v[0] == 27 && count >= 3)
        {
            int code = v[2];
            ev = code switch
            {
                13 or 10 => new KeyEvent(Key.Enter, ctrl: ctrl, alt: alt, shift: shift),
                9 => new KeyEvent(Key.Tab, ctrl: ctrl, alt: alt, shift: shift),
                27 => new KeyEvent(Key.Escape, ctrl: ctrl, alt: alt, shift: shift),
                127 or 8 => new KeyEvent(Key.Backspace, ctrl: ctrl, alt: alt, shift: shift),
                > 0 and <= char.MaxValue => new KeyEvent(Key.Char, (char)code, ctrl, alt, shift),
                _ => new KeyEvent(Key.Char, '\uFFFD', ctrl, alt, shift),
            };
            return true;
        }

        Key key = final switch
        {
            'A' => Key.Up,
            'B' => Key.Down,
            'C' => Key.Right,
            'D' => Key.Left,
            'H' => Key.Home,
            'F' => Key.End,
            'P' => Key.F1,
            'Q' => Key.F2,
            'R' => Key.F3,
            'S' => Key.F4,
            '~' => TildeKey(v[0]),
            _ => Key.None,
        };

        if (key == Key.None)
        {
            return false;
        }

        ev = new KeyEvent(key, ctrl: ctrl, alt: alt, shift: shift);
        return true;
    }

    private static Key TildeKey(int code) => code switch
    {
        1 or 7 => Key.Home,
        2 => Key.Insert,
        3 => Key.Delete,
        4 or 8 => Key.End,
        5 => Key.PageUp,
        6 => Key.PageDown,
        11 => Key.F1,
        12 => Key.F2,
        13 => Key.F3,
        14 => Key.F4,
        15 => Key.F5,
        17 => Key.F6,
        18 => Key.F7,
        19 => Key.F8,
        20 => Key.F9,
        21 => Key.F10,
        23 => Key.F11,
        24 => Key.F12,
        _ => Key.None,
    };

    // xterm modifier param: 1 + shift(1) + alt(2) + ctrl(4) + meta(8).
    private static void DecodeModifiers(int mod, out bool ctrl, out bool alt, out bool shift)
    {
        int bits = mod > 0 ? mod - 1 : 0;
        shift = (bits & 1) != 0;
        alt = (bits & 2) != 0;
        ctrl = (bits & 4) != 0;
    }

    private static int LeadingParam(ReadOnlySpan<byte> paramBytes)
    {
        int value = 0;
        bool any = false;
        foreach (byte c in paramBytes)
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

    private static int SplitParams(ReadOnlySpan<byte> paramBytes, Span<int> output)
    {
        int count = 0;
        int value = 0;
        bool any = false;
        foreach (byte b in paramBytes)
        {
            if (b is >= (byte)'0' and <= (byte)'9')
            {
                value = (value * 10) + (b - '0');
                any = true;
            }
            else if (b == (byte)';' || b == (byte)':')
            {
                if (count < output.Length)
                {
                    output[count++] = any ? value : 0;
                }

                value = 0;
                any = false;
            }
        }

        if (any || count == 0)
        {
            if (count < output.Length)
            {
                output[count++] = any ? value : 0;
            }
        }

        return count;
    }

    private static int Utf8SequenceLength(byte lead)
    {
        if ((lead & 0xE0) == 0xC0)
        {
            return 2;
        }

        if ((lead & 0xF0) == 0xE0)
        {
            return 3;
        }

        if ((lead & 0xF8) == 0xF0)
        {
            return 4;
        }

        return -1;
    }

    private byte[] CopyRange(int start, int length)
    {
        var result = new byte[length];
        for (int k = 0; k < length; k++)
        {
            result[k] = _pending[start + k];
        }

        return result;
    }

    private bool MatchesAt(int position, ReadOnlySpan<byte> needle)
    {
        for (int k = 0; k < needle.Length; k++)
        {
            if (_pending[position + k] != needle[k])
            {
                return false;
            }
        }

        return true;
    }
}
