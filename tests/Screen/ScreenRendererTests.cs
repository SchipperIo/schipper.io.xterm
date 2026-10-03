using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Tests.Screen;

[TestClass]
public sealed class ScreenRendererTests
{
    [TestMethod]
    public void Wide_rune_advances_two_columns()
    {
        var buffer = new ScreenBuffer(4, 1);
        buffer.DrawText(0, 0, "\u4e00A", Color.Default, Color.Default);
        Assert.AreEqual('\u4e00', buffer[0, 0].Glyph);
        Assert.AreEqual('\0', buffer[1, 0].Glyph);
        Assert.AreEqual('A', buffer[2, 0].Glyph);

        string spaced = ScreenRenderer.RenderComplete(buffer);
        Assert.IsFalse(spaced.Contains("\x1b[1;2H", StringComparison.Ordinal));

        var overlap = new ScreenBuffer(4, 1);
        overlap.Set(0, 0, '\u4e00', Color.Default, Color.Default);
        overlap.Set(1, 0, 'A', Color.Default, Color.Default);
        string frame = ScreenRenderer.RenderComplete(overlap);
        Assert.IsTrue(frame.Contains("\x1b[1;2H", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Control_glyph_is_not_emitted_as_esc()
    {
        var buffer = new ScreenBuffer(1, 1);
        buffer.Set(0, 0, '\u001b', Color.Default, Color.Default);
        Assert.AreEqual('\uFFFD', buffer[0, 0].Glyph);
        string frame = ScreenRenderer.RenderComplete(buffer);
        Assert.IsTrue(frame.Contains('\uFFFD'));
        Assert.IsFalse(frame.Contains("\x1b\x1b", StringComparison.Ordinal));
    }
}
