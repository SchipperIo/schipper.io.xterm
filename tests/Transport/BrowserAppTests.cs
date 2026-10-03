using System.Text;
using Schipper.Io.FileBrowser;
using Schipper.Io.Xterm.Input;
using Schipper.Io.Xterm.Layout;
using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Tests.Transport;

[TestClass]
public sealed class BrowserAppTests
{
    private const string HeaderTitle = "Schipper.Io.Xterm — File Browser";
    private const string FooterHint = "↑↓ move   ⏎/→ open   ←/⌫ up   Tab switch pane   q quit";

    [TestMethod]
    public async Task Idle_flush_turns_a_lone_escape_into_quit()
    {
        var terminal = new FakeTerminal();
        terminal.Enqueue(0x1B);
        var app = new BrowserApp(terminal);
        await app.RunAsync().WaitAsync(TimeSpan.FromSeconds(3));
    }

    [TestMethod]
    public void Bracketed_paste_does_not_activate()
    {
        var app = new BrowserApp(new FakeTerminal());
        int folders = app.FolderSel;
        int contents = app.ContentSel;
        string? path = app.CurrentPath;
        app.HandleEvents("\x1b[200~x\r\n\x1b[201~"u8);
        Assert.AreEqual(folders, app.FolderSel);
        Assert.AreEqual(contents, app.ContentSel);
        Assert.AreEqual(path, app.CurrentPath);
    }

    [TestMethod]
    public void Header_path_does_not_overwrite_the_title()
    {
        var terminal = new FakeTerminal(80, 24);
        var app = new BrowserApp(terminal);
        app.CurrentPath = new string('p', 60);
        app.PaintHeaderForTests();
        ScreenBuffer s = app.HeaderSurface;
        for (int i = 0; i < HeaderTitle.Length; i++)
        {
            Assert.AreEqual(HeaderTitle[i], s[1 + i, 0].Glyph);
        }

        int firstPath = -1;
        for (int x = 1 + HeaderTitle.Length; x < s.Width; x++)
        {
            if (s[x, 0].Glyph is 'p' or '…')
            {
                firstPath = x;
                break;
            }
        }

        Assert.IsTrue(firstPath >= 1 + HeaderTitle.Length + 1);
        Assert.IsTrue(firstPath >= 34);
    }

    [TestMethod]
    public void Footer_counts_stay_off_the_hint()
    {
        var terminal = new FakeTerminal(80, 24);
        var app = new BrowserApp(terminal);
        app.SeedContents(12, 34);
        app.PaintFooterForTests();
        ScreenBuffer wide = app.FooterSurface;
        Assert.AreEqual(FooterHint[^1], wide[1 + FooterHint.Length - 1, 0].Glyph);

        terminal.Columns = 60;
        app.RebuildLayoutForTests();
        app.SeedContents(12, 34);
        app.PaintFooterForTests();
        ScreenBuffer narrow = app.FooterSurface;
        Assert.AreEqual(FooterHint[^1], narrow[1 + FooterHint.Length - 1, 0].Glyph);
        string row = ReadRow(narrow, 0);
        Assert.IsFalse(row.Contains("12 folders", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Wheel_scrolls_the_pane_under_the_pointer()
    {
        var app = new BrowserApp(new FakeTerminal());
        app.EnsureFolderCount(10);
        app.FolderSel = 5;
        app.ContentSel = 5;
        app.RightFocused = true;
        app.FolderListRect = new Rect(0, 2, 20, 10);
        app.ContentListRect = new Rect(22, 2, 20, 10);

        var input = new XtermInput();
        MouseEvent m = input.Feed(Encoding.UTF8.GetBytes("\x1b[<64;5;5M"))[0].Mouse;
        Assert.AreEqual(4, m.X);
        Assert.AreEqual(4, m.Y);
        app.HandleMouseForTests(m);
        Assert.AreEqual(2, app.FolderSel);
        Assert.AreEqual(5, app.ContentSel);
    }

    private static string ReadRow(ScreenBuffer b, int y)
    {
        var chars = new char[b.Width];
        for (int x = 0; x < b.Width; x++)
        {
            chars[x] = b[x, y].Glyph;
        }

        return new string(chars);
    }
}
