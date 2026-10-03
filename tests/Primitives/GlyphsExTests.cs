using Schipper.Io.Xterm.Primitives;

namespace Schipper.Io.Xterm.Tests.Primitives;

[TestClass]
public sealed class GlyphsExTests
{
    [TestMethod]
    public void Ramps_have_nine_steps_and_known_endpoints()
    {
        Assert.AreEqual(9, GlyphsEx.HorizontalEighths.Length);
        Assert.AreEqual(9, GlyphsEx.VerticalEighths.Length);
        Assert.AreEqual(' ', GlyphsEx.HorizontalEighths[0]);
        Assert.AreEqual('█', GlyphsEx.HorizontalEighths[8]);
        Assert.AreEqual('▁', GlyphsEx.VerticalEighths[1]);
        Assert.AreEqual('█', GlyphsEx.VerticalEighths[8]);
    }

    [TestMethod]
    public void Bar_is_exact_width_and_uses_partial_blocks()
    {
        Assert.AreEqual("██  ", GlyphsEx.Bar(0.5, 4));
        Assert.AreEqual("    ", GlyphsEx.Bar(0.0, 4));
        Assert.AreEqual("████", GlyphsEx.Bar(1.0, 4));
        // 1/16 of 4 cells = 2 eighths => a quarter-block in the first cell.
        Assert.AreEqual("▎   ", GlyphsEx.Bar(1.0 / 16, 4));
    }

    [TestMethod]
    public void Bar_clamps_out_of_range_fractions()
    {
        Assert.AreEqual("█████", GlyphsEx.Bar(2.5, 5));
        Assert.AreEqual("     ", GlyphsEx.Bar(-1.0, 5));
        Assert.AreEqual(string.Empty, GlyphsEx.Bar(0.5, 0));
    }

    [TestMethod]
    public void Sparkline_scales_between_min_and_max()
    {
        Assert.AreEqual("▁▂▃▄▅▆▇█", GlyphsEx.Sparkline([0, 1, 2, 3, 4, 5, 6, 7]));
        Assert.AreEqual(string.Empty, GlyphsEx.Sparkline([]));
    }

    [TestMethod]
    public void Sparkline_handles_flat_input()
    {
        string s = GlyphsEx.Sparkline([3, 3, 3]);
        Assert.AreEqual(3, s.Length);
        Assert.AreEqual("▄▄▄", s); // all map to the mid level
    }

    [TestMethod]
    public void Braille_composes_from_dot_bits()
    {
        Assert.AreEqual('⠀', GlyphsEx.Braille(0));        // blank braille cell
        Assert.AreEqual('⣿', GlyphsEx.Braille(0xFF));     // all eight dots
        Assert.AreEqual('⠁', GlyphsEx.Braille(GlyphsEx.BrailleDot1));
        Assert.AreEqual('⠉', GlyphsEx.Braille(GlyphsEx.BrailleDot1 | GlyphsEx.BrailleDot4));
    }

    [TestMethod]
    public void SpinnerFrame_cycles_and_handles_negatives()
    {
        Assert.AreEqual('|', GlyphsEx.SpinnerFrame(GlyphsEx.LineSpinner, 0));
        Assert.AreEqual('/', GlyphsEx.SpinnerFrame(GlyphsEx.LineSpinner, 1));
        Assert.AreEqual('|', GlyphsEx.SpinnerFrame(GlyphsEx.LineSpinner, 4));
        Assert.AreEqual('\\', GlyphsEx.SpinnerFrame(GlyphsEx.LineSpinner, -1));
    }

    [TestMethod]
    public void SpinnerFrame_rejects_empty()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GlyphsEx.SpinnerFrame(string.Empty, 0));
    }
}
