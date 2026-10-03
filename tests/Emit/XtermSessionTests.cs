using System.Text;
using Schipper.Io.Xterm.Emit;
using Schipper.Io.Xterm.Transport;

namespace Schipper.Io.Xterm.Tests.Emit;

[TestClass]
public sealed class XtermSessionTests
{
    private const string Csi = "\x1b[";

    /// <summary>Records everything written to it so a session's enter/leave bytes can be asserted.</summary>
    private sealed class RecordingTerminal : ITerminal
    {
        private readonly StringBuilder _written = new();

        public int Columns => 80;
        public int Rows => 24;
        public event Action? Resized { add { } remove { } }

        public string Written => _written.ToString();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            _written.Append(Encoding.UTF8.GetString(bytes.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }

    private sealed class ThrowingLeaveTerminal : ITerminal
    {
        private readonly StringBuilder _written = new();
        private int _writes;

        public int Columns => 80;
        public int Rows => 24;
        public event Action? Resized { add { } remove { } }
        public string Written => _written.ToString();
        public int Writes => _writes;

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            _writes++;
            if (_writes == 2)
            {
                throw new IOException("leave failed");
            }

            _written.Append(Encoding.UTF8.GetString(bytes.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }

    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [TestMethod]
    public void Default_enter_uses_altscreen_hide_cursor_autowrap_off_clear()
    {
        var session = TestSession(new XtermSessionOptions());
        string enter = Decode(session.BuildEnter());
        Assert.AreEqual($"{Csi}?1049h{Csi}?25l{Csi}?7l{Csi}0m{Csi}2J{Csi}1;1H", enter);
    }

    [TestMethod]
    public void Default_leave_is_the_inverse_in_reverse_order()
    {
        var session = TestSession(new XtermSessionOptions());
        string leave = Decode(session.BuildLeave());
        Assert.AreEqual($"{Csi}0m{Csi}?7h{Csi}?25h{Csi}?1049l", leave);
    }

    [TestMethod]
    public void Features_are_enabled_on_enter_and_disabled_on_leave()
    {
        var options = new XtermSessionOptions
        {
            MouseMode = MouseMode.ButtonEvent,
            BracketedPaste = true,
            FocusReporting = true,
            ClearOnEnter = false,
        };
        var session = TestSession(options);

        Assert.AreEqual(
            $"{Csi}?1049h{Csi}?25l{Csi}?7l{Csi}?1002h{Csi}?1006h{Csi}?2004h{Csi}?1004h",
            Decode(session.BuildEnter()));

        Assert.AreEqual(
            $"{Csi}0m{Csi}?1004l{Csi}?2004l{Csi}?1006l{Csi}?1003l{Csi}?1002l{Csi}?1000l{Csi}?7h{Csi}?25h{Csi}?1049l",
            Decode(session.BuildLeave()));
    }

    [TestMethod]
    public void Disabled_chrome_emits_nothing_extra()
    {
        var options = new XtermSessionOptions
        {
            UseAlternateScreen = false,
            HideCursor = false,
            DisableAutowrap = false,
            ClearOnEnter = false,
        };
        var session = TestSession(options);
        Assert.AreEqual(string.Empty, Decode(session.BuildEnter()));
        Assert.AreEqual($"{Csi}0m", Decode(session.BuildLeave()));
    }

    [TestMethod]
    public async Task BeginAsync_writes_enter_and_DisposeAsync_writes_leave_once()
    {
        var terminal = new RecordingTerminal();
        var options = new XtermSessionOptions { ClearOnEnter = false };
        XtermSession session = await XtermSession.BeginAsync(terminal, options);
        string afterEnter = terminal.Written;

        await session.DisposeAsync();
        await session.DisposeAsync(); // idempotent

        string leaveOnly = terminal.Written[afterEnter.Length..];
        Assert.AreEqual($"{Csi}?1049h{Csi}?25l{Csi}?7l", afterEnter);
        Assert.AreEqual($"{Csi}0m{Csi}?7h{Csi}?25h{Csi}?1049l", leaveOnly);
    }

    [TestMethod]
    public async Task BeginAsync_rejects_null_terminal()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await XtermSession.BeginAsync(null!));
    }

    [TestMethod]
    public async Task DisposeAsync_retries_leave_after_a_failed_write()
    {
        var terminal = new ThrowingLeaveTerminal();
        var options = new XtermSessionOptions { ClearOnEnter = false };
        XtermSession session = await XtermSession.BeginAsync(terminal, options);
        string afterEnter = terminal.Written;

        await Assert.ThrowsExactlyAsync<IOException>(async () => await session.DisposeAsync());
        await session.DisposeAsync();

        string leaveOnly = terminal.Written[afterEnter.Length..];
        Assert.AreEqual(3, terminal.Writes);
        Assert.AreEqual($"{Csi}0m{Csi}?7h{Csi}?25h{Csi}?1049l", leaveOnly);
    }

    private static XtermSession TestSession(XtermSessionOptions options)
    {
        // BeginAsync against a recording terminal gives us a started session whose Build* we assert.
        var terminal = new RecordingTerminal();
        return XtermSession.BeginAsync(terminal, options).AsTask().GetAwaiter().GetResult();
    }
}
