namespace Schipper.Io.Xterm.Layout;

/// <summary>Where a <see cref="Region"/> sits in its <see cref="ScreenLayout"/>.</summary>
public enum RegionPlacement
{
    /// <summary>A fixed-height band stacked from the top, in add order.</summary>
    Top,

    /// <summary>A fixed-height band stacked from the bottom, in add order.</summary>
    Bottom,

    /// <summary>Takes the space left between the top and bottom bands.</summary>
    Fill,

    /// <summary>A centered, fixed-size overlay drawn on top of everything (a modal).</summary>
    Modal,
}
