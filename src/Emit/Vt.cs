using System.Text;

namespace Schipper.Io.Xterm.Emit;

/// <summary>
/// Modern xterm escape-sequence helpers that build on the base <see cref="Ansi"/> helpers: OSC
/// (title, hyperlinks, clipboard), DECSCUSR cursor styles, synchronized output, and the private
/// modes for mouse / bracketed-paste / focus reporting and autowrap. Every method appends to a
/// <see cref="StringBuilder"/> so a whole frame (or a setup/teardown burst) can be composed before
/// flushing, exactly like <see cref="Ansi"/>.
/// </summary>
public static class Vt
{
    /// <summary>OSC introducer: <c>ESC ]</c>.</summary>
    public const string Osc = "\x1b]";

    /// <summary>String Terminator: <c>ESC \</c>.</summary>
    public const string St = "\x1b\\";

    /// <summary>BEL, the legacy OSC terminator accepted by virtually every terminal.</summary>
    public const char Bel = '\x07';

    // ---- Autowrap (DECAWM, private mode 7) ------------------------------------------------

    /// <summary>Disables autowrap so writing the bottom-right cell does not scroll the screen.</summary>
    public static StringBuilder DisableAutowrap(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?7l");

    /// <summary>Re-enables autowrap.</summary>
    public static StringBuilder EnableAutowrap(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?7h");

    // ---- OSC: title / hyperlink / clipboard -----------------------------------------------

    /// <summary>Sets the window and icon title (OSC 0).</summary>
    public static StringBuilder SetTitle(StringBuilder sb, string title) =>
        sb.Append(Osc).Append("0;").Append(title).Append(Bel);

    /// <summary>
    /// Emits an OSC 8 hyperlink: <paramref name="text"/> rendered as a link to <paramref name="uri"/>.
    /// An optional <paramref name="id"/> lets a terminal treat multiple spans as one logical link.
    /// </summary>
    public static StringBuilder Hyperlink(StringBuilder sb, string uri, string text, string? id = null)
    {
        sb.Append(Osc).Append("8;");
        if (!string.IsNullOrEmpty(id))
        {
            sb.Append("id=").Append(id);
        }

        sb.Append(';').Append(uri).Append(St);
        sb.Append(text);
        sb.Append(Osc).Append("8;;").Append(St); // close the link
        return sb;
    }

    /// <summary>
    /// Copies <paramref name="text"/> to the terminal clipboard (OSC 52). <paramref name="selection"/>
    /// is the target: <c>c</c> = clipboard, <c>p</c> = primary. The text is base64-encoded UTF-8 per
    /// the spec.
    /// </summary>
    public static StringBuilder SetClipboard(StringBuilder sb, string text, char selection = 'c')
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        return sb.Append(Osc).Append("52;").Append(selection).Append(';').Append(encoded).Append(St);
    }

    // ---- Cursor style (DECSCUSR) ----------------------------------------------------------

    /// <summary>Selects the cursor shape (DECSCUSR <c>CSI Ps SP q</c>).</summary>
    public static StringBuilder SetCursorStyle(StringBuilder sb, CursorStyle style) =>
        sb.Append(Ansi.Csi).Append((int)style).Append(" q");

    // ---- Synchronized output (mode 2026) --------------------------------------------------

    /// <summary>Begins a synchronized-output frame so the terminal paints it atomically (mode 2026).</summary>
    public static StringBuilder BeginSync(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?2026h");

    /// <summary>Ends a synchronized-output frame, flushing it to the screen (mode 2026).</summary>
    public static StringBuilder EndSync(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?2026l");

    // ---- Mouse (DEC modes 1000/1002/1003 + SGR ext 1006) ----------------------------------

    /// <summary>
    /// Enables mouse tracking in the requested <paramref name="mode"/> together with the SGR (1006)
    /// extended encoding (no 223-column limit). <see cref="MouseMode.Off"/> emits nothing.
    /// </summary>
    public static StringBuilder EnableMouse(StringBuilder sb, MouseMode mode)
    {
        if (mode == MouseMode.Off)
        {
            return sb;
        }

        sb.Append(Ansi.Csi).Append('?').Append((int)mode).Append('h');
        sb.Append(Ansi.Csi).Append("?1006h");
        return sb;
    }

    /// <summary>Disables all mouse tracking modes and the SGR extended encoding.</summary>
    public static StringBuilder DisableMouse(StringBuilder sb)
    {
        sb.Append(Ansi.Csi).Append("?1006l");
        sb.Append(Ansi.Csi).Append("?1003l");
        sb.Append(Ansi.Csi).Append("?1002l");
        sb.Append(Ansi.Csi).Append("?1000l");
        return sb;
    }

    // ---- Bracketed paste (mode 2004) ------------------------------------------------------

    /// <summary>Enables bracketed paste so pasted text is wrapped in <c>CSI 200~ … CSI 201~</c>.</summary>
    public static StringBuilder EnableBracketedPaste(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?2004h");

    /// <summary>Disables bracketed paste.</summary>
    public static StringBuilder DisableBracketedPaste(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?2004l");

    // ---- Focus reporting (mode 1004) ------------------------------------------------------

    /// <summary>Enables focus in/out reporting (<c>CSI I</c> / <c>CSI O</c>).</summary>
    public static StringBuilder EnableFocusReporting(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?1004h");

    /// <summary>Disables focus in/out reporting.</summary>
    public static StringBuilder DisableFocusReporting(StringBuilder sb) => sb.Append(Ansi.Csi).Append("?1004l");

    // ---- Reset ----------------------------------------------------------------------------

    /// <summary>Soft terminal reset (DECSTR <c>CSI ! p</c>): restores most modes to their defaults.</summary>
    public static StringBuilder SoftReset(StringBuilder sb) => sb.Append(Ansi.Csi).Append("!p");
}
