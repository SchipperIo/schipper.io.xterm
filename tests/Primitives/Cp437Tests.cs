using Schipper.Io.Xterm.Primitives;

namespace Schipper.Io.Xterm.Tests.Primitives;

[TestClass]
public sealed class Cp437Tests
{
    [TestMethod]
    public void Byte_0xff_is_nbsp_and_space_stays_0x20()
    {
        Assert.AreEqual('\u00A0', Cp437.ToChar(0xFF));
        Assert.AreEqual(0xFF, Cp437.FromChar('\u00A0'));
        Assert.AreEqual(0x20, Cp437.FromChar(' '));
        Assert.AreEqual('\u0020', Cp437.ToChar(0x00));
    }
}
