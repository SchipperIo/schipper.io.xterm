using System.Text;
using Schipper.Io.Xterm.Emit;

namespace Schipper.Io.Xterm.Tests.Emit;

[TestClass]
public sealed class VtTests
{
    private const string Esc = "\x1b";
    private const string Csi = "\x1b[";

    private static string Build(Action<StringBuilder> emit)
    {
        var sb = new StringBuilder();
        emit(sb);
        return sb.ToString();
    }

    [TestMethod]
    public void SetTitle_emits_osc0_bel()
    {
        Assert.AreEqual($"{Esc}]0;hello\x07", Build(sb => Vt.SetTitle(sb, "hello")));
    }

    [TestMethod]
    public void Hyperlink_wraps_text_with_osc8_open_and_close()
    {
        string result = Build(sb => Vt.Hyperlink(sb, "https://x.io", "click"));
        Assert.AreEqual($"{Esc}]8;;https://x.io{Esc}\\click{Esc}]8;;{Esc}\\", result);
    }

    [TestMethod]
    public void Hyperlink_includes_id_when_given()
    {
        string result = Build(sb => Vt.Hyperlink(sb, "https://x.io", "click", id: "42"));
        Assert.AreEqual($"{Esc}]8;id=42;https://x.io{Esc}\\click{Esc}]8;;{Esc}\\", result);
    }

    [TestMethod]
    public void SetClipboard_base64_encodes_utf8()
    {
        string expected64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("hi"));
        Assert.AreEqual($"{Esc}]52;c;{expected64}{Esc}\\", Build(sb => Vt.SetClipboard(sb, "hi")));
        Assert.AreEqual($"{Esc}]52;p;{expected64}{Esc}\\", Build(sb => Vt.SetClipboard(sb, "hi", selection: 'p')));
    }

    [TestMethod]
    public void SetCursorStyle_emits_decscusr()
    {
        Assert.AreEqual($"{Csi}5 q", Build(sb => Vt.SetCursorStyle(sb, CursorStyle.BlinkingBar)));
        Assert.AreEqual($"{Csi}2 q", Build(sb => Vt.SetCursorStyle(sb, CursorStyle.SteadyBlock)));
    }

    [TestMethod]
    public void Sync_begin_and_end_use_mode_2026()
    {
        Assert.AreEqual($"{Csi}?2026h", Build(sb => Vt.BeginSync(sb)));
        Assert.AreEqual($"{Csi}?2026l", Build(sb => Vt.EndSync(sb)));
    }

    [TestMethod]
    public void EnableMouse_combines_tracking_mode_with_sgr_1006()
    {
        Assert.AreEqual($"{Csi}?1002h{Csi}?1006h", Build(sb => Vt.EnableMouse(sb, MouseMode.ButtonEvent)));
        Assert.AreEqual($"{Csi}?1000h{Csi}?1006h", Build(sb => Vt.EnableMouse(sb, MouseMode.Click)));
    }

    [TestMethod]
    public void EnableMouse_off_emits_nothing()
    {
        Assert.AreEqual(string.Empty, Build(sb => Vt.EnableMouse(sb, MouseMode.Off)));
    }

    [TestMethod]
    public void DisableMouse_clears_every_tracking_mode()
    {
        Assert.AreEqual($"{Csi}?1006l{Csi}?1003l{Csi}?1002l{Csi}?1000l", Build(sb => Vt.DisableMouse(sb)));
    }

    [TestMethod]
    public void BracketedPaste_and_focus_modes()
    {
        Assert.AreEqual($"{Csi}?2004h", Build(sb => Vt.EnableBracketedPaste(sb)));
        Assert.AreEqual($"{Csi}?2004l", Build(sb => Vt.DisableBracketedPaste(sb)));
        Assert.AreEqual($"{Csi}?1004h", Build(sb => Vt.EnableFocusReporting(sb)));
        Assert.AreEqual($"{Csi}?1004l", Build(sb => Vt.DisableFocusReporting(sb)));
    }

    [TestMethod]
    public void Autowrap_and_soft_reset()
    {
        Assert.AreEqual($"{Csi}?7l", Build(sb => Vt.DisableAutowrap(sb)));
        Assert.AreEqual($"{Csi}?7h", Build(sb => Vt.EnableAutowrap(sb)));
        Assert.AreEqual($"{Csi}!p", Build(sb => Vt.SoftReset(sb)));
    }
}
