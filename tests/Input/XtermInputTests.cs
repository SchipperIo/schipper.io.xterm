using System.Text;
using Schipper.Io.Xterm.Input;

namespace Schipper.Io.Xterm.Tests.Input;

[TestClass]
public sealed class XtermInputTests
{
    private static List<TerminalEvent> Feed(XtermInput input, string ascii) =>
        new(input.Feed(Encoding.UTF8.GetBytes(ascii)));

    private static List<TerminalEvent> FeedOnce(string ascii) => Feed(new XtermInput(), ascii);

    // ---- plain keys (delegated to InputParser) --------------------------------------------

    [TestMethod]
    public void Plain_char_is_a_key_event()
    {
        List<TerminalEvent> events = FeedOnce("a");
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(TerminalEventKind.Key, events[0].Kind);
        Assert.AreEqual('a', events[0].Key.Char);
    }

    [TestMethod]
    public void Plain_arrow_up_is_delegated()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[A");
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Up, events[0].Key.Key);
        Assert.IsFalse(events[0].Key.Ctrl);
    }

    [TestMethod]
    public void Ss3_function_key_is_delegated()
    {
        List<TerminalEvent> events = FeedOnce("\x1bOP");
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.F1, events[0].Key.Key);
    }

    // ---- modified keys (CSI-u and modified CSI) -------------------------------------------

    [TestMethod]
    public void Modified_csi_arrow_carries_ctrl()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[1;5A"); // Ctrl+Up
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Up, events[0].Key.Key);
        Assert.IsTrue(events[0].Key.Ctrl);
        Assert.IsFalse(events[0].Key.Shift);
    }

    [TestMethod]
    public void Modified_tilde_key_carries_shift()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[3;2~"); // Shift+Delete
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Delete, events[0].Key.Key);
        Assert.IsTrue(events[0].Key.Shift);
    }

    [TestMethod]
    public void CsiU_char_with_ctrl_shift()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[97;6u"); // 'a' + ctrl(4)+shift(1) => mod 6
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('a', events[0].Key.Char);
        Assert.IsTrue(events[0].Key.Ctrl);
        Assert.IsTrue(events[0].Key.Shift);
        Assert.IsFalse(events[0].Key.Alt);
    }

    [TestMethod]
    public void CsiU_enter()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[13u");
        Assert.AreEqual(Key.Enter, events[0].Key.Key);
    }

    // ---- mouse (SGR 1006) -----------------------------------------------------------------

    [TestMethod]
    public void Mouse_left_press_decodes_zero_based_coords()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[<0;12;24M");
        Assert.AreEqual(1, events.Count);
        MouseEvent m = events[0].Mouse;
        Assert.AreEqual(TerminalEventKind.Mouse, events[0].Kind);
        Assert.AreEqual(MouseButton.Left, m.Button);
        Assert.AreEqual(MouseAction.Press, m.Action);
        Assert.AreEqual(11, m.X);
        Assert.AreEqual(23, m.Y);
    }

    [TestMethod]
    public void Mouse_release_uses_final_m()
    {
        MouseEvent m = FeedOnce("\x1b[<0;5;5m")[0].Mouse;
        Assert.AreEqual(MouseAction.Release, m.Action);
        Assert.AreEqual(MouseButton.Left, m.Button);
    }

    [TestMethod]
    public void Mouse_wheel_up_and_down()
    {
        Assert.AreEqual(MouseButton.WheelUp, FeedOnce("\x1b[<64;1;1M")[0].Mouse.Button);
        Assert.AreEqual(MouseButton.WheelDown, FeedOnce("\x1b[<65;1;1M")[0].Mouse.Button);
    }

    [TestMethod]
    public void Mouse_drag_is_a_move_with_button()
    {
        MouseEvent m = FeedOnce("\x1b[<32;3;4M")[0].Mouse; // motion bit set, low bits 0 = left
        Assert.AreEqual(MouseAction.Move, m.Action);
        Assert.AreEqual(MouseButton.Left, m.Button);
    }

    [TestMethod]
    public void Mouse_modifiers_decode()
    {
        // base button 0 + shift(4) + ctrl(16) = 20
        MouseEvent m = FeedOnce("\x1b[<20;1;1M")[0].Mouse;
        Assert.IsTrue(m.Shift);
        Assert.IsTrue(m.Ctrl);
        Assert.IsFalse(m.Alt);
    }

    // ---- focus ----------------------------------------------------------------------------

    [TestMethod]
    public void Focus_in_and_out()
    {
        Assert.IsTrue(FeedOnce("\x1b[I")[0].Focused);
        Assert.AreEqual(TerminalEventKind.Focus, FeedOnce("\x1b[I")[0].Kind);
        Assert.IsFalse(FeedOnce("\x1b[O")[0].Focused);
    }

    // ---- bracketed paste ------------------------------------------------------------------

    [TestMethod]
    public void Paste_block_is_one_event()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[200~hello world\x1b[201~");
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(TerminalEventKind.Paste, events[0].Kind);
        Assert.AreEqual("hello world", events[0].PasteText);
    }

    [TestMethod]
    public void Paste_preserves_utf8_and_control_bytes()
    {
        List<TerminalEvent> events = FeedOnce("\x1b[200~café\tx\x1b[201~");
        Assert.AreEqual("café\tx", events[0].PasteText);
    }

    [TestMethod]
    public void Paste_split_across_feeds_waits_for_end_marker()
    {
        var input = new XtermInput();
        List<TerminalEvent> first = Feed(input, "\x1b[200~par");
        Assert.AreEqual(0, first.Count); // incomplete — buffered

        List<TerminalEvent> second = Feed(input, "tial\x1b[201~");
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual("partial", second[0].PasteText);
    }

    // ---- partial sequences across reads ---------------------------------------------------

    [TestMethod]
    public void Mouse_split_across_feeds()
    {
        var input = new XtermInput();
        Assert.AreEqual(0, Feed(input, "\x1b[<0;12").Count);
        List<TerminalEvent> rest = Feed(input, ";24M");
        Assert.AreEqual(1, rest.Count);
        Assert.AreEqual(11, rest[0].Mouse.X);
    }

    [TestMethod]
    public void Lone_escape_flushes_to_escape_key()
    {
        var input = new XtermInput();
        Assert.AreEqual(0, Feed(input, "\x1b").Count);
        Assert.IsTrue(input.PendingIsLoneEscape);
        TerminalEvent? flushed = input.Flush();
        Assert.IsNotNull(flushed);
        Assert.AreEqual(Key.Escape, flushed.Value.Key.Key);
    }

    [TestMethod]
    public void Flush_leaves_a_partial_mouse_report()
    {
        var input = new XtermInput();
        Assert.AreEqual(0, Feed(input, "\x1b[<0;12").Count);
        Assert.IsNull(input.Flush());
        Assert.IsTrue(input.PendingCount > 1);
        List<TerminalEvent> rest = Feed(input, ";24M");
        Assert.AreEqual(1, rest.Count);
        Assert.AreEqual(11, rest[0].Mouse.X);
        Assert.AreEqual(23, rest[0].Mouse.Y);
    }

    [TestMethod]
    public void Flush_leaves_a_partial_paste()
    {
        var input = new XtermInput();
        Assert.AreEqual(0, Feed(input, "\x1b[200~hel").Count);
        Assert.IsNull(input.Flush());
        List<TerminalEvent> rest = Feed(input, "lo\x1b[201~");
        Assert.AreEqual(1, rest.Count);
        Assert.AreEqual("hello", rest[0].PasteText);
        Assert.AreNotEqual(Key.Escape, rest[0].Kind == TerminalEventKind.Key ? rest[0].Key.Key : Key.None);
    }

    [TestMethod]
    public void Abort_open_csi_on_a_new_escape()
    {
        var input = new XtermInput();
        Assert.AreEqual(0, Feed(input, "\x1b[<0;5;5").Count);
        List<TerminalEvent> rest = Feed(input, "\x1b[A");
        Assert.AreEqual(1, rest.Count);
        Assert.AreEqual(Key.Up, rest[0].Key.Key);
        Assert.AreEqual(0, input.PendingCount);
        Assert.AreEqual(0, rest.Count(static e => e.Kind == TerminalEventKind.Mouse));

        List<TerminalEvent> complete = FeedOnce("\x1b[<0;5;5M");
        Assert.AreEqual(MouseButton.Left, complete[0].Mouse.Button);
        Assert.AreEqual(4, complete[0].Mouse.X);
        Assert.AreEqual(4, complete[0].Mouse.Y);
    }

    [TestMethod]
    public void Modify_other_keys_form_2()
    {
        List<TerminalEvent> ctrlA = FeedOnce("\x1b[27;5;97~");
        Assert.AreEqual('a', ctrlA[0].Key.Char);
        Assert.IsTrue(ctrlA[0].Key.Ctrl);
        Assert.IsFalse(ctrlA[0].Key.Shift);

        List<TerminalEvent> ctrlShiftA = FeedOnce("\x1b[27;6;65~");
        Assert.AreEqual('A', ctrlShiftA[0].Key.Char);
        Assert.IsTrue(ctrlShiftA[0].Key.Ctrl);
        Assert.IsTrue(ctrlShiftA[0].Key.Shift);

        List<TerminalEvent> shiftDelete = FeedOnce("\x1b[3;2~");
        Assert.AreEqual(Key.Delete, shiftDelete[0].Key.Key);
        Assert.IsTrue(shiftDelete[0].Key.Shift);
    }

    [TestMethod]
    public void Sgr_buttons_8_through_11_are_not_left()
    {
        MouseEvent extended = FeedOnce("\x1b[<128;2;3M")[0].Mouse;
        Assert.AreNotEqual(MouseButton.Left, extended.Button);
        Assert.AreEqual(MouseButton.None, extended.Button);
        Assert.AreEqual(1, extended.X);
        Assert.AreEqual(2, extended.Y);

        MouseEvent left = FeedOnce("\x1b[<0;2;3M")[0].Mouse;
        Assert.AreEqual(MouseButton.Left, left.Button);
    }

    // ---- mixed stream ---------------------------------------------------------------------

    [TestMethod]
    public void Mixed_stream_decodes_each_unit_in_order()
    {
        List<TerminalEvent> events = FeedOnce("a\x1b[<0;1;1Mb\x1b[I");
        Assert.AreEqual(4, events.Count);
        Assert.AreEqual('a', events[0].Key.Char);
        Assert.AreEqual(TerminalEventKind.Mouse, events[1].Kind);
        Assert.AreEqual('b', events[2].Key.Char);
        Assert.AreEqual(TerminalEventKind.Focus, events[3].Kind);
    }
}
