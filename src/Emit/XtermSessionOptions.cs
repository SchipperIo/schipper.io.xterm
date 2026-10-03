namespace Schipper.Io.Xterm.Emit;

/// <summary>
/// Configuration for an <see cref="XtermSession"/>. Every option has a sensible default for a
/// full-screen TUI; flip the ones you don't want.
/// </summary>
public sealed class XtermSessionOptions
{
    /// <summary>Switch to the alternate screen buffer on enter, restore the normal one on exit.</summary>
    public bool UseAlternateScreen { get; set; } = true;

    /// <summary>Hide the cursor for the duration of the session.</summary>
    public bool HideCursor { get; set; } = true;

    /// <summary>Disable autowrap so writing the bottom-right cell does not scroll the screen.</summary>
    public bool DisableAutowrap { get; set; } = true;

    /// <summary>Clear the screen and home the cursor on enter.</summary>
    public bool ClearOnEnter { get; set; } = true;

    /// <summary>Mouse tracking mode to enable; <see cref="MouseMode.Off"/> leaves the mouse alone.</summary>
    public MouseMode MouseMode { get; set; } = MouseMode.Off;

    /// <summary>Enable bracketed paste (DEC mode 2004).</summary>
    public bool BracketedPaste { get; set; }

    /// <summary>Enable focus in/out reporting (DEC mode 1004).</summary>
    public bool FocusReporting { get; set; }
}
