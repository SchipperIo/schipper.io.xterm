namespace Schipper.Io.Xterm.Input;

/// <summary>What happened to the mouse button in a <see cref="MouseEvent"/>.</summary>
public enum MouseAction
{
    /// <summary>A button went down, or a wheel ticked.</summary>
    Press,

    /// <summary>A button was released.</summary>
    Release,

    /// <summary>The pointer moved (drag when a button is held, or bare motion in any-event mode).</summary>
    Move,
}
