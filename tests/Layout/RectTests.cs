using Schipper.Io.Xterm.Layout;

namespace Schipper.Io.Xterm.Tests.Layout;

[TestClass]
public sealed class RectTests
{
    [TestMethod]
    public void Edges_and_contains()
    {
        var r = new Rect(2, 3, 10, 5);
        Assert.AreEqual(12, r.Right);
        Assert.AreEqual(8, r.Bottom);
        Assert.IsTrue(r.Contains(2, 3));
        Assert.IsTrue(r.Contains(11, 7));
        Assert.IsFalse(r.Contains(12, 7)); // right edge is exclusive
        Assert.IsFalse(r.Contains(1, 3));
    }

    [TestMethod]
    public void Negative_extents_clamp_to_zero()
    {
        var r = new Rect(0, 0, -5, -2);
        Assert.IsTrue(r.IsEmpty);
        Assert.AreEqual(0, r.Width);
    }

    [TestMethod]
    public void Inset_shrinks_all_sides()
    {
        var r = new Rect(0, 0, 10, 10).Inset(1);
        Assert.AreEqual(new Rect(1, 1, 8, 8), r);
    }

    [TestMethod]
    public void Inset_never_goes_negative()
    {
        var r = new Rect(0, 0, 4, 4).Inset(10);
        Assert.IsTrue(r.IsEmpty);
    }

    [TestMethod]
    public void SplitTop_partitions_height()
    {
        (Rect band, Rect remainder) = new Rect(0, 0, 80, 24).SplitTop(3);
        Assert.AreEqual(new Rect(0, 0, 80, 3), band);
        Assert.AreEqual(new Rect(0, 3, 80, 21), remainder);
    }

    [TestMethod]
    public void SplitBottom_partitions_height()
    {
        (Rect remainder, Rect band) = new Rect(0, 0, 80, 24).SplitBottom(1);
        Assert.AreEqual(new Rect(0, 0, 80, 23), remainder);
        Assert.AreEqual(new Rect(0, 23, 80, 1), band);
    }

    [TestMethod]
    public void Split_clamps_oversized_request()
    {
        (Rect band, Rect remainder) = new Rect(0, 0, 10, 4).SplitTop(99);
        Assert.AreEqual(4, band.Height);
        Assert.IsTrue(remainder.IsEmpty);
    }

    [TestMethod]
    public void Centered_places_inside()
    {
        Rect c = new Rect(0, 0, 80, 24).Centered(20, 6);
        Assert.AreEqual(new Rect(30, 9, 20, 6), c);
    }

    [TestMethod]
    public void Centered_clamps_to_container()
    {
        Rect c = new Rect(0, 0, 10, 10).Centered(40, 40);
        Assert.AreEqual(new Rect(0, 0, 10, 10), c);
    }
}
