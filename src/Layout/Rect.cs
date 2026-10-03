namespace Schipper.Io.Xterm.Layout;

/// <summary>
/// An axis-aligned rectangle in zero-based cell coordinates: <see cref="X"/>/<see cref="Y"/> is the
/// top-left corner, <see cref="Right"/>/<see cref="Bottom"/> are exclusive. Immutable; the helper
/// methods return new rectangles.
/// </summary>
public readonly struct Rect : IEquatable<Rect>
{
    public Rect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }

    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>The exclusive right edge (<see cref="X"/> + <see cref="Width"/>).</summary>
    public int Right => X + Width;

    /// <summary>The exclusive bottom edge (<see cref="Y"/> + <see cref="Height"/>).</summary>
    public int Bottom => Y + Height;

    /// <summary>True when the rectangle has no area.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>True when (<paramref name="x"/>,<paramref name="y"/>) lies inside the rectangle.</summary>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>Shrinks the rectangle by <paramref name="amount"/> on every side (negative grows it).</summary>
    public Rect Inset(int amount) => Inset(amount, amount, amount, amount);

    /// <summary>Shrinks by independent margins on each side, never producing negative extents.</summary>
    public Rect Inset(int left, int top, int right, int bottom)
    {
        int x = X + left;
        int y = Y + top;
        int w = Width - left - right;
        int h = Height - top - bottom;
        return new Rect(x, y, Math.Max(0, w), Math.Max(0, h));
    }

    /// <summary>Splits off a band of <paramref name="height"/> rows from the top; returns (band, remainder).</summary>
    public (Rect Band, Rect Remainder) SplitTop(int height)
    {
        int h = Math.Clamp(height, 0, Height);
        var band = new Rect(X, Y, Width, h);
        var remainder = new Rect(X, Y + h, Width, Height - h);
        return (band, remainder);
    }

    /// <summary>Splits off a band of <paramref name="height"/> rows from the bottom; returns (remainder, band).</summary>
    public (Rect Remainder, Rect Band) SplitBottom(int height)
    {
        int h = Math.Clamp(height, 0, Height);
        var remainder = new Rect(X, Y, Width, Height - h);
        var band = new Rect(X, Y + Height - h, Width, h);
        return (remainder, band);
    }

    /// <summary>Returns a <paramref name="width"/>×<paramref name="height"/> rectangle centered inside this one.</summary>
    public Rect Centered(int width, int height)
    {
        int w = Math.Min(width, Width);
        int h = Math.Min(height, Height);
        int x = X + ((Width - w) / 2);
        int y = Y + ((Height - h) / 2);
        return new Rect(x, y, w, h);
    }

    public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is Rect r && Equals(r);

    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);

    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    public override string ToString() => $"({X},{Y} {Width}x{Height})";
}
