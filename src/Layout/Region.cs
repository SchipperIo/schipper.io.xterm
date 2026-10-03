using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Layout;

/// <summary>
/// One area of a <see cref="ScreenLayout"/>. The layout solves its outer <see cref="Bounds"/>; the
/// drawable <see cref="Content"/> is <see cref="Bounds"/> minus the border, and <see cref="Surface"/>
/// is a <see cref="ScreenBuffer"/> sized to that content for the app to paint into. The app can read
/// the solved size and renegotiate it via <see cref="RequestHeight"/> / the border properties.
/// </summary>
public sealed class Region
{
    private readonly ScreenLayout _owner;
    private BorderStyle _border;
    private BorderSides _borderSides;
    private ScreenBuffer _surface = new(1, 1);

    internal Region(ScreenLayout owner, RegionPlacement placement, int requestedWidth, int requestedHeight, BorderStyle border, BorderSides borderSides, string? name)
    {
        _owner = owner;
        Placement = placement;
        RequestedWidth = Math.Max(0, requestedWidth);
        RequestedHeight = Math.Max(0, requestedHeight);
        _border = border;
        _borderSides = borderSides;
        Name = name;
    }

    /// <summary>An optional caller-supplied label (handy for lookups and debugging).</summary>
    public string? Name { get; }

    public RegionPlacement Placement { get; }

    /// <summary>Foreground color the layout uses when drawing this region's border.</summary>
    public Color BorderForeground { get; set; } = Color.Default;

    /// <summary>Background color the layout uses when drawing this region's border.</summary>
    public Color BorderBackground { get; set; } = Color.Default;

    /// <summary>Requested outer width — only meaningful for <see cref="RegionPlacement.Modal"/>.</summary>
    public int RequestedWidth { get; private set; }

    /// <summary>Requested outer height — meaningful for <see cref="RegionPlacement.Top"/>/<see cref="RegionPlacement.Bottom"/>/<see cref="RegionPlacement.Modal"/>.</summary>
    public int RequestedHeight { get; private set; }

    /// <summary>The border style; assigning re-solves the layout (the content size may change).</summary>
    public BorderStyle Border
    {
        get => _border;
        set
        {
            if (_border != value)
            {
                _border = value;
                _owner.Solve();
            }
        }
    }

    /// <summary>Which sides are bordered; assigning re-solves the layout.</summary>
    public BorderSides BorderSides
    {
        get => _borderSides;
        set
        {
            if (_borderSides != value)
            {
                _borderSides = value;
                _owner.Solve();
            }
        }
    }

    /// <summary>The outer rectangle the layout assigned (including any border).</summary>
    public Rect Bounds { get; private set; }

    /// <summary>The drawable rectangle: <see cref="Bounds"/> minus the border insets.</summary>
    public Rect Content { get; private set; }

    /// <summary>True when there is at least one drawable cell.</summary>
    public bool HasContent => !Content.IsEmpty;

    /// <summary>Width of the drawable content area.</summary>
    public int ContentWidth => Content.Width;

    /// <summary>Height of the drawable content area.</summary>
    public int ContentHeight => Content.Height;

    /// <summary>A buffer sized to <see cref="Content"/> for the app to paint into; recreated on re-solve. Empty content uses a zero-sided buffer.</summary>
    public ScreenBuffer Surface => _surface;

    /// <summary>
    /// Requests a new outer height (for Top/Bottom/Modal regions), re-solves, and returns the height
    /// actually granted — clamped to the space available, so the caller learns what it really got.
    /// </summary>
    public int RequestHeight(int rows)
    {
        RequestedHeight = Math.Max(0, rows);
        _owner.Solve();
        return Bounds.Height;
    }

    /// <summary>Requests a new outer width (Modal regions only), re-solves, and returns the granted width.</summary>
    public int RequestWidth(int columns)
    {
        RequestedWidth = Math.Max(0, columns);
        _owner.Solve();
        return Bounds.Width;
    }

    // Called by the layout after it computes this region's outer rectangle.
    internal void Place(Rect bounds)
    {
        Bounds = bounds;

        int left = Inset(BorderSides.Left);
        int top = Inset(BorderSides.Top);
        int right = Inset(BorderSides.Right);
        int bottom = Inset(BorderSides.Bottom);
        Content = bounds.Inset(left, top, right, bottom);

        int w = Content.Width;
        int h = Content.Height;
        if (_surface.Width != w || _surface.Height != h)
        {
            _surface = new ScreenBuffer(w, h);
        }
        else
        {
            _surface.Clear();
        }
    }

    private int Inset(BorderSides side) => _border != BorderStyle.None && (_borderSides & side) != 0 ? 1 : 0;
}
