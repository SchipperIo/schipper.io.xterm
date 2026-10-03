namespace Schipper.Io.Xterm.Emit;

/// <summary>The shape of the terminal cursor, selected with DECSCUSR (<c>CSI Ps SP q</c>).</summary>
public enum CursorStyle
{
    /// <summary>The terminal's configured default.</summary>
    Default = 0,
    BlinkingBlock = 1,
    SteadyBlock = 2,
    BlinkingUnderline = 3,
    SteadyUnderline = 4,
    BlinkingBar = 5,
    SteadyBar = 6,
}
