using Schipper.Io.Xterm.Transport;

namespace Schipper.Io.Xterm.Tests.Transport;

// These exercise only the safe paths: under the test host stdin/stdout are redirected, so
// ConsoleTerminal never touches the real console mode and construction/dispose are side-effect free.
[TestClass]
public sealed class ConsoleTerminalTests
{
    [TestMethod]
    public void Constructs_and_reports_a_usable_size()
    {
        using var terminal = new ConsoleTerminal();
        Assert.IsTrue(terminal.Columns > 0);
        Assert.IsTrue(terminal.Rows > 0);
    }

    [TestMethod]
    public void Is_an_ITerminal_and_IDisposable()
    {
        using var terminal = new ConsoleTerminal();
        Assert.IsInstanceOfType<ITerminal>(terminal);
        Assert.IsInstanceOfType<IDisposable>(terminal);
    }

    [TestMethod]
    public void Dispose_is_idempotent()
    {
        var terminal = new ConsoleTerminal();
        terminal.Dispose();
        terminal.Dispose(); // must not throw
    }

    [TestMethod]
    public void Raw_input_mode_sets_extended_flags_and_clears_quick_edit()
    {
        const uint enableProcessedInput = 0x0001;
        const uint enableLineInput = 0x0002;
        const uint enableEchoInput = 0x0004;
        const uint enableQuickEditMode = 0x0040;
        const uint enableExtendedFlags = 0x0080;
        const uint enableVirtualTerminalInput = 0x0200;

        uint current = enableProcessedInput | enableLineInput | enableEchoInput | enableQuickEditMode;
        uint raw = ConsoleTerminal.BuildRawInputMode(current);
        Assert.AreEqual(0u, raw & enableProcessedInput);
        Assert.AreEqual(0u, raw & enableLineInput);
        Assert.AreEqual(0u, raw & enableEchoInput);
        Assert.AreEqual(0u, raw & enableQuickEditMode);
        Assert.AreEqual(enableExtendedFlags, raw & enableExtendedFlags);
        Assert.AreEqual(enableVirtualTerminalInput, raw & enableVirtualTerminalInput);
    }
}
