using Schipper.Io.Xterm.Input;

namespace Schipper.Io.Xterm.Tests.Input;

[TestClass]
public sealed class InputParserTests
{
    [TestMethod]
    public void Utf8_keystroke_is_one_character()
    {
        var parser = new InputParser();
        IReadOnlyList<KeyEvent> once = parser.Feed([0xC3, 0xA9]);
        Assert.AreEqual(1, once.Count);
        Assert.AreEqual('é', once[0].Char);

        var split = new InputParser();
        Assert.AreEqual(0, split.Feed([0xC3]).Count);
        IReadOnlyList<KeyEvent> rest = split.Feed([0xA9]);
        Assert.AreEqual(1, rest.Count);
        Assert.AreEqual('é', rest[0].Char);
    }

    [TestMethod]
    public void Invalid_utf8_is_replacement()
    {
        IReadOnlyList<KeyEvent> events = new InputParser().Feed([0xFF]);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('\uFFFD', events[0].Char);
    }

    [TestMethod]
    public void Ctrl_space_and_high_c0()
    {
        var parser = new InputParser();
        IReadOnlyList<KeyEvent> events = parser.Feed([0x00, 0x1C, 0x1D, 0x1E, 0x1F]);
        Assert.AreEqual(5, events.Count);
        Assert.AreEqual(' ', events[0].Char);
        Assert.AreEqual('\\', events[1].Char);
        Assert.AreEqual(']', events[2].Char);
        Assert.AreEqual('^', events[3].Char);
        Assert.AreEqual('_', events[4].Char);
        foreach (KeyEvent ev in events)
        {
            Assert.IsTrue(ev.Ctrl);
            Assert.AreEqual(Key.Char, ev.Key);
        }

        Assert.AreEqual(Key.Tab, parser.Feed([0x09])[0].Key);
        Assert.AreEqual(0, parser.Feed([0x1B]).Count);
        Assert.IsTrue(parser.PendingIsLoneEscape);
    }

    [TestMethod]
    public void Split_crlf_is_one_enter()
    {
        var together = new InputParser();
        IReadOnlyList<KeyEvent> one = together.Feed([0x0D, 0x0A]);
        Assert.AreEqual(1, one.Count);
        Assert.AreEqual(Key.Enter, one[0].Key);

        var split = new InputParser();
        Assert.AreEqual(0, split.Feed([0x0D]).Count);
        IReadOnlyList<KeyEvent> second = split.Feed([0x0A]);
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual(Key.Enter, second[0].Key);
        Assert.AreEqual(0, split.PendingCount);

        var crThenA = new InputParser();
        Assert.AreEqual(0, crThenA.Feed([0x0D]).Count);
        IReadOnlyList<KeyEvent> pair = crThenA.Feed([0x41]);
        Assert.AreEqual(2, pair.Count);
        Assert.AreEqual(Key.Enter, pair[0].Key);
        Assert.AreEqual('A', pair[1].Char);
    }

    [TestMethod]
    public void Flush_held_cr_is_enter()
    {
        var parser = new InputParser();
        Assert.AreEqual(0, parser.Feed([0x0D]).Count);
        KeyEvent? flushed = parser.Flush();
        Assert.IsNotNull(flushed);
        Assert.AreEqual(Key.Enter, flushed.Value.Key);
    }

    [TestMethod]
    public void Flush_leaves_partial_csi()
    {
        var parser = new InputParser();
        Assert.AreEqual(0, parser.Feed([0x1B, (byte)'[', (byte)'1', (byte)'5']).Count);
        Assert.IsNull(parser.Flush());
        Assert.IsTrue(parser.PendingCount > 1);
        IReadOnlyList<KeyEvent> rest = parser.Feed([(byte)'~']);
        Assert.AreEqual(Key.F5, rest[0].Key);
    }
}
