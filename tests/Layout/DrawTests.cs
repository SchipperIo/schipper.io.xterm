using Schipper.Io.Xterm.Layout;
using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Tests.Layout;

[TestClass]
public sealed class DrawTests
{
    private static string Row(ScreenBuffer b, int y)
    {
        var chars = new char[b.Width];
        for (int x = 0; x < b.Width; x++)
        {
            chars[x] = b[x, y].Glyph;
        }

        return new string(chars);
    }

    [TestMethod]
    public void Box_single_draws_corners_and_edges()
    {
        var b = new ScreenBuffer(5, 3);
        Draw.Box(b, new Rect(0, 0, 5, 3), BorderStyle.Single, Color.Default, Color.Default);
        Assert.AreEqual("┌───┐", Row(b, 0));
        Assert.AreEqual("│   │", Row(b, 1));
        Assert.AreEqual("└───┘", Row(b, 2));
    }

    [TestMethod]
    public void Box_top_only_has_no_sides_or_bottom()
    {
        var b = new ScreenBuffer(4, 3);
        Draw.Box(b, new Rect(0, 0, 4, 3), BorderStyle.Single, Color.Default, Color.Default, BorderSides.Top);
        Assert.AreEqual("────", Row(b, 0));
        Assert.AreEqual("    ", Row(b, 1));
        Assert.AreEqual("    ", Row(b, 2));
    }

    [TestMethod]
    public void Box_ascii_style()
    {
        var b = new ScreenBuffer(3, 3);
        Draw.Box(b, new Rect(0, 0, 3, 3), BorderStyle.Ascii, Color.Default, Color.Default);
        Assert.AreEqual("+-+", Row(b, 0));
        Assert.AreEqual("| |", Row(b, 1));
        Assert.AreEqual("+-+", Row(b, 2));
    }

    [TestMethod]
    public void Text_alignment()
    {
        var b = new ScreenBuffer(7, 1);
        Draw.Text(b, new Rect(0, 0, 7, 1), "hi", Color.Default, Color.Default, TextAlign.Right);
        Assert.AreEqual("     hi", Row(b, 0));

        b.Clear();
        Draw.Text(b, new Rect(0, 0, 7, 1), "hi", Color.Default, Color.Default, TextAlign.Center);
        Assert.AreEqual("  hi   ", Row(b, 0));
    }

    [TestMethod]
    public void Text_clips_to_width()
    {
        var b = new ScreenBuffer(4, 1);
        Draw.Text(b, new Rect(0, 0, 4, 1), "abcdefg", Color.Default, Color.Default);
        Assert.AreEqual("abcd", Row(b, 0));
    }

    [TestMethod]
    public void Header_fills_background_and_centers_title()
    {
        var b = new ScreenBuffer(9, 1);
        Draw.Header(b, new Rect(0, 0, 9, 1), "Menu", Color.Default, Color.Basic(BasicColor.Blue));
        Assert.AreEqual("  Menu   ", Row(b, 0));
        // background applied across the whole bar
        Assert.AreEqual(Color.Basic(BasicColor.Blue), b[0, 0].Background);
    }

    [TestMethod]
    public void DimBackground_adds_faint_everywhere()
    {
        var b = new ScreenBuffer(3, 1);
        b.DrawText(0, 0, "abc", Color.Default, Color.Default);
        Draw.DimBackground(b);
        for (int x = 0; x < 3; x++)
        {
            Assert.IsTrue((b[x, 0].Attributes & CellAttributes.Faint) != 0);
        }
    }

    [TestMethod]
    public void Modal_dims_backdrop_and_draws_titled_panel()
    {
        var b = new ScreenBuffer(10, 5);
        b.Fill(new Cell('.', Color.Default, Color.Default));
        Draw.Modal(b, new Rect(2, 1, 6, 3), BorderStyle.Single, Color.Default, Color.Default, title: "Hi");

        // backdrop dimmed
        Assert.IsTrue((b[0, 0].Attributes & CellAttributes.Faint) != 0);
        // panel corners
        Assert.AreEqual('┌', b[2, 1].Glyph);
        Assert.AreEqual('┘', b[7, 3].Glyph);
        // title centered on the top edge
        Assert.AreEqual(" Hi ", new string([b[3, 1].Glyph, b[4, 1].Glyph, b[5, 1].Glyph, b[6, 1].Glyph]));
        // interior cleared (not the dimmed '.')
        Assert.AreEqual(' ', b[3, 2].Glyph);
    }

    [TestMethod]
    public void Splash_centers_content_buffer()
    {
        var b = new ScreenBuffer(8, 4);
        var content = new ScreenBuffer(2, 2);
        content.Fill(new Cell('#', Color.Default, Color.Default));
        Draw.Splash(b, content);
        Assert.AreEqual('#', b[3, 1].Glyph);
        Assert.AreEqual('#', b[4, 2].Glyph);
        Assert.AreEqual(' ', b[0, 0].Glyph);
    }

    [TestMethod]
    public void Splash_centers_text_lines()
    {
        var b = new ScreenBuffer(9, 4);
        Draw.Splash(b, new[] { "AAAA", "BB" }, Color.Default, Color.Default);
        // 2-line block centered vertically in 4 rows starts at row 1.
        Assert.IsTrue(Row(b, 1).Contains("AAAA", StringComparison.Ordinal));
        Assert.IsTrue(Row(b, 2).Contains("BB", StringComparison.Ordinal));
    }
}
