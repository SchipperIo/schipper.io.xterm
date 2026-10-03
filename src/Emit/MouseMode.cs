namespace Schipper.Io.Xterm.Emit;

/// <summary>Which mouse-tracking mode to request when enabling SGR (1006) mouse reporting.</summary>
public enum MouseMode
{
    /// <summary>No mouse tracking.</summary>
    Off = 0,

    /// <summary>Report button press and release only (DEC mode 1000).</summary>
    Click = 1000,

    /// <summary>Report presses/releases plus motion while a button is held (DEC mode 1002).</summary>
    ButtonEvent = 1002,

    /// <summary>Report all motion, even with no button held (DEC mode 1003).</summary>
    AnyEvent = 1003,
}
