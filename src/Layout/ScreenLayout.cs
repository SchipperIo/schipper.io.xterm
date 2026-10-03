using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Layout;

/// <summary>
/// A retained layout over a terminal-sized area. The app declares regions — fixed-height bands at
/// the top/bottom and a fill region in the middle — plus a stack of centered modals. The layout
/// solves each region's <see cref="Region.Bounds"/>/<see cref="Region.Content"/>, hands the app a
/// <see cref="Region.Surface"/> to paint into, and <see cref="Compose"/> blits everything (with
/// borders, and a dim backdrop under modals) into a target <see cref="ScreenBuffer"/> for rendering.
///
/// <para>Regions report their solved size and can renegotiate it (<see cref="Region.RequestHeight"/>,
/// the border properties), so an app can ask for "a 3-line footer with a top border" and learn what
/// it was actually granted.</para>
/// </summary>
public sealed class ScreenLayout
{
    private readonly List<Region> _regions = new();
    private readonly List<Region> _modals = new();

    public ScreenLayout(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>The full layout area as a rectangle.</summary>
    public Rect Area => new(0, 0, Width, Height);

    /// <summary>The base regions (top/bottom/fill) in add order.</summary>
    public IReadOnlyList<Region> Regions => _regions;

    /// <summary>The modal stack, bottom-most first.</summary>
    public IReadOnlyList<Region> Modals => _modals;

    /// <summary>The top-most modal, or null when none are open.</summary>
    public Region? ActiveModal => _modals.Count > 0 ? _modals[^1] : null;

    /// <summary>Looks up a region by <see cref="Region.Name"/>; throws if absent.</summary>
    public Region this[string name] =>
        Find(name) ?? throw new KeyNotFoundException($"No region named '{name}'.");

    /// <summary>Finds a base region by name, or null.</summary>
    public Region? Find(string name)
    {
        foreach (Region r in _regions)
        {
            if (r.Name == name)
            {
                return r;
            }
        }

        return null;
    }

    /// <summary>Adds a fixed-height band at the top (below any earlier top bands) and re-solves.</summary>
    public Region AddTop(int height, BorderStyle border = BorderStyle.None, BorderSides sides = BorderSides.All, string? name = null) =>
        Add(new Region(this, RegionPlacement.Top, Width, height, border, sides, name));

    /// <summary>Adds a fixed-height band at the bottom (above any earlier bottom bands) and re-solves.</summary>
    public Region AddBottom(int height, BorderStyle border = BorderStyle.None, BorderSides sides = BorderSides.All, string? name = null) =>
        Add(new Region(this, RegionPlacement.Bottom, Width, height, border, sides, name));

    /// <summary>Adds (or shares) the fill region between the top and bottom bands and re-solves.</summary>
    public Region AddFill(BorderStyle border = BorderStyle.None, BorderSides sides = BorderSides.All, string? name = null) =>
        Add(new Region(this, RegionPlacement.Fill, Width, 0, border, sides, name));

    /// <summary>Pushes a centered modal of the given size onto the overlay stack and re-solves.</summary>
    public Region PushModal(int width, int height, BorderStyle border = BorderStyle.Single, BorderSides sides = BorderSides.All, string? name = null)
    {
        var modal = new Region(this, RegionPlacement.Modal, width, height, border, sides, name);
        _modals.Add(modal);
        Solve();
        return modal;
    }

    /// <summary>Pops the top-most modal. Returns false when the stack was already empty.</summary>
    public bool PopModal()
    {
        if (_modals.Count == 0)
        {
            return false;
        }

        _modals.RemoveAt(_modals.Count - 1);
        return true;
    }

    /// <summary>Changes the layout size (e.g. on a terminal window resize) and re-solves every region.</summary>
    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Solve();
    }

    /// <summary>Blits all regions (borders + surfaces) and any modals (over a dim backdrop) into the target.</summary>
    public void Compose(ScreenBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        foreach (Region r in _regions)
        {
            DrawRegion(target, r);
        }

        if (_modals.Count > 0)
        {
            Draw.DimBackground(target);
            foreach (Region m in _modals)
            {
                DrawRegion(target, m);
            }
        }
    }

    /// <summary>Creates a chrome-free layout that is one fill region spanning the whole area.</summary>
    public static ScreenLayout Fullscreen(int width, int height)
    {
        var layout = new ScreenLayout(width, height);
        layout.AddFill(name: "content");
        return layout;
    }

    // Solves every region's bounds. Internal because Region triggers it when its size/border changes.
    internal void Solve()
    {
        int topY = 0;
        int bottomY = Height;

        foreach (Region r in _regions)
        {
            switch (r.Placement)
            {
                case RegionPlacement.Top:
                {
                    int h = Math.Clamp(r.RequestedHeight, 0, bottomY - topY);
                    r.Place(new Rect(0, topY, Width, h));
                    topY += h;
                    break;
                }

                case RegionPlacement.Bottom:
                {
                    int h = Math.Clamp(r.RequestedHeight, 0, bottomY - topY);
                    bottomY -= h;
                    r.Place(new Rect(0, bottomY, Width, h));
                    break;
                }
            }
        }

        SolveFills(topY, bottomY);

        foreach (Region m in _modals)
        {
            m.Place(Area.Centered(m.RequestedWidth, m.RequestedHeight));
        }
    }

    private void SolveFills(int topY, int bottomY)
    {
        int count = 0;
        foreach (Region r in _regions)
        {
            if (r.Placement == RegionPlacement.Fill)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        int remaining = Math.Max(0, bottomY - topY);
        int each = remaining / count;
        int extra = remaining % count;

        int y = topY;
        int seen = 0;
        foreach (Region r in _regions)
        {
            if (r.Placement != RegionPlacement.Fill)
            {
                continue;
            }

            seen++;
            int h = each + (seen == count ? extra : 0); // last fill absorbs the remainder
            r.Place(new Rect(0, y, Width, h));
            y += h;
        }
    }

    private static void DrawRegion(ScreenBuffer target, Region region)
    {
        if (region.Bounds.IsEmpty)
        {
            return;
        }

        Draw.Box(target, region.Bounds, region.Border, region.BorderForeground, region.BorderBackground, region.BorderSides);
        if (region.HasContent)
        {
            target.Blit(region.Surface, region.Content.X, region.Content.Y);
        }
    }

    private Region Add(Region region)
    {
        _regions.Add(region);
        Solve();
        return region;
    }
}
