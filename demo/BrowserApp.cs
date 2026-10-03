using Schipper.Io.Xterm.Input;
using Schipper.Io.Xterm.Layout;
using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;
using Schipper.Io.Xterm.Transport;

namespace Schipper.Io.FileBrowser;

/// <summary>
/// A read-only two-pane file browser. The left pane navigates the folder tree (drives → folders →
/// up); the right pane lists the current folder's contents with a Created column and extension-based
/// coloring. Keyboard and mouse both work. Built entirely on Schipper.Io.Xterm: <see cref="ScreenLayout"/>
/// for the header/body/footer chrome, immediate-mode <see cref="Draw"/> for the panes, <see cref="XtermInput"/>
/// for events, and <see cref="ScreenRenderer"/> for minimal-diff output.
/// </summary>
internal sealed class BrowserApp
{
    private const int DateWidth = 16; // "yyyy-MM-dd HH:mm"
    private const int IdleFlushMs = 80;
    private const string HeaderTitle = "Schipper.Io.Xterm — File Browser";
    private const string FooterHint = "↑↓ move   ⏎/→ open   ←/⌫ up   Tab switch pane   q quit";

    private static readonly Color HeaderBg = Color.Basic(BasicColor.Cyan);
    private static readonly Color HeaderFg = Color.Basic(BasicColor.Black);
    private static readonly Color FooterBg = Color.Basic(BasicColor.BrightBlack);
    private static readonly Color FooterFg = Color.Basic(BasicColor.BrightWhite);
    private static readonly Color SelFocusBg = Color.Basic(BasicColor.Cyan);
    private static readonly Color SelFocusFg = Color.Basic(BasicColor.Black);
    private static readonly Color SelBlurBg = Color.Basic(BasicColor.BrightBlack);
    private static readonly Color SelBlurFg = Color.Basic(BasicColor.BrightWhite);

    private readonly ITerminal _terminal;
    private readonly XtermInput _input = new();

    private ScreenLayout _layout = null!;
    private Region _header = null!;
    private Region _body = null!;
    private Region _footer = null!;
    private ScreenBuffer _screen = null!;
    private ScreenBuffer? _previous;

    // null current path = the "Computer" root (the drive list).
    private string? _current;
    private List<Entry> _folders = new(); // left pane (navigable)
    private List<Entry> _contents = new(); // right pane (all entries, with metadata)

    private int _folderSel, _folderScroll, _folderHeight = 1;
    private int _contentSel, _contentScroll, _contentHeight = 1;
    private bool _rightFocused;

    // List areas in screen coordinates, captured each render for mouse hit-testing.
    private Rect _folderListRect;
    private Rect _contentListRect;

    private volatile bool _pendingResize;
    private volatile TaskCompletionSource _wake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running;

    public BrowserApp(ITerminal terminal)
    {
        _terminal = terminal;

        // The terminal's resize poller fires on a background thread; flag it and wake the loop so a
        // repaint happens immediately instead of waiting for the next keypress.
        _terminal.Resized += () =>
        {
            _pendingResize = true;
            _wake.TrySetResult();
        };

        RebuildLayout();
        Load(Directory.GetCurrentDirectory());
    }

    public async Task RunAsync()
    {
        _running = true;
        bool dirty = true;
        var buffer = new byte[4096];
        Task<int>? readTask = null;

        while (_running)
        {
            if (_pendingResize)
            {
                _pendingResize = false;
                RebuildLayout();
                dirty = true;
            }

            if (dirty)
            {
                await RenderAsync().ConfigureAwait(false);
                dirty = false;
            }

            // Keep a single outstanding read and race it against a resize wake-up, so resizing
            // repaints right away without needing input to unblock the read. A short idle timeout
            // finishes a lone ESC without flushing a longer partial CSI or paste.
            readTask ??= _terminal.ReadAsync(buffer).AsTask();
            TaskCompletionSource wake = _wake;
            Task completed;
            if (_input.PendingIsLoneEscape)
            {
                Task idle = Task.Delay(IdleFlushMs);
                completed = await Task.WhenAny(readTask, wake.Task, idle).ConfigureAwait(false);
                if (completed == idle)
                {
                    TerminalEvent? flushed = _input.Flush();
                    if (flushed is { } ev)
                    {
                        HandleEvent(ev);
                        dirty = true;
                    }

                    continue;
                }
            }
            else
            {
                completed = await Task.WhenAny(readTask, wake.Task).ConfigureAwait(false);
            }

            if (completed != readTask)
            {
                // Woken by a resize: install a fresh signal; the loop top applies the pending resize.
                _wake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                continue;
            }

            int n = await readTask.ConfigureAwait(false);
            readTask = null;
            if (n == 0)
            {
                break; // input stream closed
            }

            foreach (TerminalEvent ev in _input.Feed(buffer.AsSpan(0, n)))
            {
                HandleEvent(ev);
                dirty = true;
            }
        }
    }

    // ---- input ---------------------------------------------------------------------------

    private void HandleEvent(TerminalEvent ev)
    {
        switch (ev.Kind)
        {
            case TerminalEventKind.Key:
                HandleKey(ev.Key);
                break;

            case TerminalEventKind.Mouse:
                HandleMouse(ev.Mouse);
                break;
        }
    }

    private void HandleKey(KeyEvent k)
    {
        if (k.Key == Key.Escape || (k.IsChar && char.ToLowerInvariant(k.Char) == 'q'))
        {
            _running = false;
            return;
        }

        switch (k.Key)
        {
            case Key.Tab:
                _rightFocused = !_rightFocused;
                break;
            case Key.Up:
                Move(-1);
                break;
            case Key.Down:
                Move(1);
                break;
            case Key.PageUp:
                Move(-PageStep());
                break;
            case Key.PageDown:
                Move(PageStep());
                break;
            case Key.Home:
                Select(0);
                break;
            case Key.End:
                Select(int.MaxValue);
                break;
            case Key.Enter:
            case Key.Right:
                Activate();
                break;
            case Key.Left:
            case Key.Backspace:
                GoUp();
                break;
        }
    }

    private void HandleMouse(MouseEvent m)
    {
        if (m.Button is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
        {
            if (m.Action == MouseAction.Release)
            {
                return;
            }

            int delta = m.Button switch
            {
                MouseButton.WheelUp => -3,
                MouseButton.WheelDown => 3,
                _ => 0,
            };
            if (delta == 0)
            {
                return;
            }

            if (_folderListRect.Contains(m.X, m.Y))
            {
                _folderSel = Clamp(_folderSel + delta, _folders.Count);
            }
            else if (_contentListRect.Contains(m.X, m.Y))
            {
                _contentSel = Clamp(_contentSel + delta, _contents.Count);
            }
            else
            {
                Move(delta);
            }

            return;
        }

        if (m.Action == MouseAction.Press && m.Button == MouseButton.Left)
        {
            if (_folderListRect.Contains(m.X, m.Y))
            {
                _rightFocused = false;
                int idx = _folderScroll + (m.Y - _folderListRect.Y);
                if (idx < _folders.Count)
                {
                    _folderSel = idx;
                }
            }
            else if (_contentListRect.Contains(m.X, m.Y))
            {
                _rightFocused = true;
                int idx = _contentScroll + (m.Y - _contentListRect.Y);
                if (idx < _contents.Count)
                {
                    _contentSel = idx;
                }
            }
        }
    }

    private void Move(int delta)
    {
        if (_rightFocused)
        {
            _contentSel = Clamp(_contentSel + delta, _contents.Count);
        }
        else
        {
            _folderSel = Clamp(_folderSel + delta, _folders.Count);
        }
    }

    private void Select(int index)
    {
        if (_rightFocused)
        {
            _contentSel = Clamp(index, _contents.Count);
        }
        else
        {
            _folderSel = Clamp(index, _folders.Count);
        }
    }

    private int PageStep() => Math.Max(1, (_rightFocused ? _contentHeight : _folderHeight) - 1);

    private void Activate()
    {
        Entry? e = _rightFocused ? At(_contents, _contentSel) : At(_folders, _folderSel);
        if (e is null)
        {
            return;
        }

        if (e.IsUp)
        {
            GoUp();
        }
        else if (e.IsDirectory || e.IsDrive)
        {
            Load(e.FullPath);
        }
    }

    private void GoUp()
    {
        if (_current is null)
        {
            return;
        }

        Load(Directory.GetParent(_current)?.FullName); // null → Computer root
    }

    // ---- model ---------------------------------------------------------------------------

    private void Load(string? path)
    {
        _current = path;
        _folders = new List<Entry>();
        _contents = new List<Entry>();

        if (path is null)
        {
            foreach (DriveInfo d in SafeDrives())
            {
                var drive = new Entry(d.Name, d.Name, IsDirectory: true, DateTime.MinValue, IsDrive: true);
                _folders.Add(drive);
                _contents.Add(drive);
            }
        }
        else
        {
            _folders.Add(new Entry("..", path, IsDirectory: true, DateTime.MinValue, IsUp: true));

            foreach (string dir in SafeEnumerate(path, directories: true))
            {
                var e = new Entry(Path.GetFileName(dir), dir, IsDirectory: true, CreatedOf(dir));
                _folders.Add(e);
                _contents.Add(e);
            }

            foreach (string file in SafeEnumerate(path, directories: false))
            {
                long size = 0;
                try
                {
                    size = new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }

                _contents.Add(new Entry(Path.GetFileName(file), file, IsDirectory: false, CreatedOf(file), size));
            }
        }

        _folderSel = _folderScroll = 0;
        _contentSel = _contentScroll = 0;
    }

    private static IEnumerable<DriveInfo> SafeDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (DriveInfo d in drives)
        {
            bool ready;
            try
            {
                ready = d.IsReady;
            }
            catch (IOException)
            {
                ready = false;
            }

            if (ready)
            {
                yield return d;
            }
        }
    }

    private static List<string> SafeEnumerate(string path, bool directories)
    {
        try
        {
            IEnumerable<string> items = directories
                ? Directory.EnumerateDirectories(path)
                : Directory.EnumerateFiles(path);
            var list = new List<string>(items);
            list.Sort(static (a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
            return list;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return new List<string>();
        }
    }

    private static DateTime CreatedOf(string path)
    {
        try
        {
            return File.GetCreationTime(path);
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
    }

    // ---- render --------------------------------------------------------------------------

    private void RebuildLayout()
    {
        int w = Math.Max(1, _terminal.Columns);
        int h = Math.Max(1, _terminal.Rows);

        _layout = new ScreenLayout(w, h);
        _header = _layout.AddTop(1, name: "header");
        _footer = _layout.AddBottom(1, name: "footer");
        _body = _layout.AddFill(name: "body");
        _screen = new ScreenBuffer(w, h);
        _previous = null;
    }

    private async Task RenderAsync()
    {
        PaintHeader();
        PaintFooter();

        _screen.Clear();
        _layout.Compose(_screen);

        Rect body = _body.Bounds;
        int leftWidth = Math.Clamp(body.Width / 3, 22, 46);
        if (leftWidth > body.Width - 12)
        {
            leftWidth = Math.Max(1, body.Width / 2);
        }

        var leftRect = new Rect(body.X, body.Y, leftWidth, body.Height);
        var rightRect = new Rect(body.X + leftWidth, body.Y, body.Width - leftWidth, body.Height);

        // Left pane: the folder navigator.
        PaintPaneBox(leftRect, "Folders", focused: !_rightFocused);
        Rect lc = leftRect.Inset(1);
        _folderHeight = lc.Height;
        _folderListRect = lc;
        EnsureVisible(ref _folderScroll, _folderSel, lc.Height);
        PaintList(lc, _folders, _folderSel, _folderScroll, focused: !_rightFocused, showDate: false);

        // Right pane: contents with a Name | Created header row.
        PaintPaneBox(rightRect, "Contents", focused: _rightFocused);
        Rect rc = rightRect.Inset(1);
        PaintColumns(rc);
        var rlist = new Rect(rc.X, rc.Y + 1, rc.Width, Math.Max(0, rc.Height - 1));
        _contentHeight = rlist.Height;
        _contentListRect = rlist;
        EnsureVisible(ref _contentScroll, _contentSel, rlist.Height);
        PaintList(rlist, _contents, _contentSel, _contentScroll, focused: _rightFocused, showDate: true);

        string frame = ScreenRenderer.RenderDiff(_previous, _screen);
        if (frame.Length > 0)
        {
            await _terminal.WriteAsync(frame).ConfigureAwait(false);
        }

        _previous = _screen.Clone();
    }

    private void PaintHeader()
    {
        ScreenBuffer s = _header.Surface;
        s.Fill(new Cell(' ', HeaderFg, HeaderBg));
        s.DrawText(1, 0, HeaderTitle, HeaderFg, HeaderBg, CellAttributes.Bold);

        string path = _current ?? "Computer";
        int titleEnd = 1 + HeaderTitle.Length;
        int reserved = HeaderTitle.Length + 2;
        int avail = s.Width - reserved;
        if (avail >= 4)
        {
            string shown = TruncateLeft(path, avail);
            int x = Math.Max(titleEnd + 1, s.Width - shown.Length - 1);
            s.DrawText(x, 0, shown, HeaderFg, HeaderBg);
        }
    }

    private void PaintFooter()
    {
        ScreenBuffer s = _footer.Surface;
        s.Fill(new Cell(' ', FooterFg, FooterBg));
        s.DrawText(1, 0, FooterHint, FooterFg, FooterBg);

        int dirs = _contents.Count(static e => e.IsDirectory && !e.IsUp);
        int files = _contents.Count - dirs;
        string counts = $"{dirs} folders · {files} files ";
        if (s.Width - counts.Length > FooterHint.Length + 1)
        {
            s.DrawText(s.Width - counts.Length, 0, counts, FooterFg, FooterBg);
        }
    }

    private void PaintPaneBox(Rect r, string title, bool focused)
    {
        Color border = focused ? Color.Basic(BasicColor.BrightCyan) : Color.Basic(BasicColor.BrightBlack);
        Draw.Box(_screen, r, BorderStyle.Single, border, Color.Default);
        if (r.Width > 4)
        {
            Color titleColor = focused ? Color.Basic(BasicColor.BrightWhite) : Color.Basic(BasicColor.White);
            _screen.DrawText(r.X + 2, r.Y, $" {title} ", titleColor, Color.Default, CellAttributes.Bold);
        }
    }

    private void PaintColumns(Rect rc)
    {
        if (rc.IsEmpty)
        {
            return;
        }

        Color head = Color.Basic(BasicColor.BrightBlack);
        _screen.DrawText(rc.X + 2, rc.Y, "Name", head, Color.Default, CellAttributes.Underline);
        if (rc.Width > DateWidth + 6)
        {
            _screen.DrawText(rc.Right - DateWidth, rc.Y, "Created", head, Color.Default, CellAttributes.Underline);
        }
    }

    private void PaintList(Rect area, List<Entry> items, int sel, int scroll, bool focused, bool showDate)
    {
        if (area.IsEmpty)
        {
            return;
        }

        bool roomForDate = showDate && area.Width > DateWidth + 8;
        int dateCol = roomForDate ? DateWidth : 0;

        for (int i = 0; i < area.Height; i++)
        {
            int idx = scroll + i;
            if (idx >= items.Count)
            {
                break;
            }

            int y = area.Y + i;
            Entry e = items[idx];
            bool selected = idx == sel;

            Color fg = ExtensionColors.For(e);
            Color bg = Color.Default;
            var attr = e.IsDirectory ? CellAttributes.Bold : CellAttributes.None;

            if (selected)
            {
                bg = focused ? SelFocusBg : SelBlurBg;
                fg = focused ? SelFocusFg : SelBlurFg;
                attr = CellAttributes.Bold;
                for (int x = area.X; x < area.Right; x++)
                {
                    _screen.Set(x, y, ' ', fg, bg);
                }
            }

            char glyph = e.IsUp ? '↑' : e.IsDrive ? '■' : e.IsDirectory ? '▸' : '·';
            Color glyphColor = selected ? fg
                : e.IsDirectory ? Color.Basic(BasicColor.BrightBlue)
                : Color.Basic(BasicColor.BrightBlack);
            _screen.DrawText(area.X + 1, y, glyph.ToString(), glyphColor, bg);

            int nameX = area.X + 3;
            int nameWidth = area.Right - nameX - (dateCol > 0 ? dateCol + 1 : 0);
            if (nameWidth > 0)
            {
                _screen.DrawText(nameX, y, Fit(e.Name, nameWidth), fg, bg, attr);
            }

            if (dateCol > 0 && e.Created != DateTime.MinValue)
            {
                Color dateColor = selected ? fg : Color.Basic(BasicColor.BrightBlack);
                _screen.DrawText(area.Right - DateWidth, y, e.Created.ToString("yyyy-MM-dd HH:mm"), dateColor, bg);
            }
        }
    }

    // ---- helpers -------------------------------------------------------------------------

    internal int FolderSel
    {
        get => _folderSel;
        set => _folderSel = Clamp(value, _folders.Count);
    }

    internal int ContentSel
    {
        get => _contentSel;
        set => _contentSel = Clamp(value, _contents.Count);
    }

    internal bool RightFocused
    {
        get => _rightFocused;
        set => _rightFocused = value;
    }

    internal Rect FolderListRect
    {
        get => _folderListRect;
        set => _folderListRect = value;
    }

    internal Rect ContentListRect
    {
        get => _contentListRect;
        set => _contentListRect = value;
    }

    internal string? CurrentPath
    {
        get => _current;
        set => _current = value;
    }

    internal ScreenBuffer HeaderSurface => _header.Surface;

    internal ScreenBuffer FooterSurface => _footer.Surface;

    internal int FolderCount => _folders.Count;

    internal void EnsureFolderCount(int count)
    {
        while (_folders.Count < count)
        {
            _folders.Add(new Entry($"folder{_folders.Count}", "x", IsDirectory: true, DateTime.MinValue));
        }
    }

    internal void RebuildLayoutForTests() => RebuildLayout();

    internal void SeedContents(int directories, int files)
    {
        _contents = new List<Entry>();
        for (int i = 0; i < directories; i++)
        {
            _contents.Add(new Entry($"dir{i}", "x", IsDirectory: true, DateTime.MinValue));
        }

        for (int i = 0; i < files; i++)
        {
            _contents.Add(new Entry($"file{i}.txt", "x", IsDirectory: false, DateTime.MinValue));
        }
    }

    internal void PaintHeaderForTests() => PaintHeader();

    internal void PaintFooterForTests() => PaintFooter();

    internal void HandleMouseForTests(MouseEvent m) => HandleMouse(m);

    internal void HandleEvents(ReadOnlySpan<byte> bytes)
    {
        foreach (TerminalEvent ev in _input.Feed(bytes))
        {
            HandleEvent(ev);
        }
    }

    private static Entry? At(List<Entry> list, int index) =>
        index >= 0 && index < list.Count ? list[index] : null;

    private static int Clamp(int value, int count) =>
        count == 0 ? 0 : Math.Clamp(value, 0, count - 1);

    private static void EnsureVisible(ref int scroll, int sel, int height)
    {
        if (height <= 0)
        {
            scroll = 0;
            return;
        }

        if (sel < scroll)
        {
            scroll = sel;
        }
        else if (sel >= scroll + height)
        {
            scroll = sel - height + 1;
        }

        if (scroll < 0)
        {
            scroll = 0;
        }
    }

    private static string Fit(string text, int width) =>
        text.Length <= width ? text : width <= 1 ? text[..width] : text[..(width - 1)] + "…";

    private static string TruncateLeft(string text, int width) =>
        text.Length <= width ? text : "…" + text[^(width - 1)..];
}
