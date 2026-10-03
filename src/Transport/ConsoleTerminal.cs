using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Schipper.Io.Xterm.Transport;

/// <summary>
/// An <see cref="ITerminal"/> backed by the local console (stdin/stdout) — the batteries-included
/// transport for a standalone console app. On construction it puts a real, attached console
/// into raw VT mode (Windows: <c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c> on output and
/// <c>ENABLE_VIRTUAL_TERMINAL_INPUT</c> with line-input/echo disabled on input; Unix: <c>stty raw
/// -echo</c>) so that the modern input stream <see cref="XtermInput"/> expects (SGR mouse, bracketed
/// paste, focus, CSI-u) actually reaches the app. <see cref="Dispose"/> restores the original mode.
///
/// <para>Everything is best-effort and guarded: when stdin/stdout is redirected (e.g. a test host or
/// a pipe) no console mode is touched, so constructing this is always safe.</para>
///
/// <para>Pair it with an <see cref="XtermSession"/> for the alt-screen / cursor / feature dance:
/// <c>using var terminal = new ConsoleTerminal(); await using var s = await XtermSession.BeginAsync(terminal, opts);</c></para>
/// </summary>
public sealed partial class ConsoleTerminal : ITerminal, IDisposable
{
    private const int ResizePollMs = 200;

    private readonly Stream _stdin = Console.OpenStandardInput();
    private readonly Stream _stdout = Console.OpenStandardOutput();
    private readonly Timer? _resizeTimer;

    private uint? _savedInputMode;
    private uint? _savedOutputMode;
    private uint? _savedInputCodePage;
    private uint? _savedOutputCodePage;
    private string? _savedStty;
    private int _lastWidth;
    private int _lastHeight;
    private bool _disposed;

    public ConsoleTerminal()
    {
        EnableRawMode();
        _lastWidth = Columns;
        _lastHeight = Rows;

        // Poll for size changes — neither platform surfaces a portable resize signal to managed code.
        if (!Console.IsOutputRedirected)
        {
            _resizeTimer = new Timer(_ => CheckResize(), null, ResizePollMs, ResizePollMs);
        }
    }

    /// <inheritdoc />
    public int Columns => SafeSize(static () => Console.WindowWidth, 80);

    /// <inheritdoc />
    public int Rows => SafeSize(static () => Console.WindowHeight, 24);

    /// <inheritdoc />
    public event Action? Resized;

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
        WriteCoreAsync(bytes, cancellationToken);

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _stdin.ReadAsync(buffer, cancellationToken);

    /// <summary>Restores the original console mode. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _resizeTimer?.Dispose();
        RestoreMode();
    }

    private async ValueTask WriteCoreAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await _stdout.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await _stdout.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void CheckResize()
    {
        int w = Columns;
        int h = Rows;
        if (w != _lastWidth || h != _lastHeight)
        {
            _lastWidth = w;
            _lastHeight = h;
            Resized?.Invoke();
        }
    }

    private static int SafeSize(Func<int> get, int fallback)
    {
        try
        {
            int v = get();
            return v > 0 ? v : fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (PlatformNotSupportedException)
        {
            return fallback;
        }
    }

    private void EnableRawMode()
    {
        if (OperatingSystem.IsWindows())
        {
            EnableWindows();
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            EnableUnix();
        }
    }

    private void RestoreMode()
    {
        if (OperatingSystem.IsWindows())
        {
            RestoreWindows();
        }
        else if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && _savedStty is not null)
        {
            RunStty(_savedStty);
        }
    }

    // ---- Windows -------------------------------------------------------------------------

    /// <summary>
    /// Builds the raw Windows console input mode: virtual-terminal input on, line/echo/processed
    /// input off, extended flags on, and quick-edit off so SGR mouse reports are delivered.
    /// </summary>
    internal static uint BuildRawInputMode(uint current)
    {
        uint raw = current;
        raw &= ~(EnableLineInput | EnableEchoInput | EnableProcessedInput | EnableQuickEditMode);
        raw |= EnableVirtualTerminalInput | EnableExtendedFlags;
        return raw;
    }

    internal const uint Utf8CodePage = 65001;

    [SupportedOSPlatform("windows")]
    private void EnableWindows()
    {
        if (!Console.IsInputRedirected)
        {
            nint hIn = GetStdHandle(StdInputHandle);
            if (GetConsoleMode(hIn, out uint inMode) != 0)
            {
                _savedInputMode = inMode;
                _ = SetConsoleMode(hIn, BuildRawInputMode(inMode));
                _savedInputCodePage = GetConsoleCP();
                _ = SetConsoleCP(Utf8CodePage);
            }
        }

        if (!Console.IsOutputRedirected)
        {
            nint hOut = GetStdHandle(StdOutputHandle);
            if (GetConsoleMode(hOut, out uint outMode) != 0)
            {
                _savedOutputMode = outMode;
                uint vt = outMode | EnableVirtualTerminalProcessing | DisableNewlineAutoReturn;
                _ = SetConsoleMode(hOut, vt);
                _savedOutputCodePage = GetConsoleOutputCP();
                _ = SetConsoleOutputCP(Utf8CodePage);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private void RestoreWindows()
    {
        if (_savedInputMode is { } inMode)
        {
            _ = SetConsoleMode(GetStdHandle(StdInputHandle), inMode);
        }

        if (_savedOutputMode is { } outMode)
        {
            _ = SetConsoleMode(GetStdHandle(StdOutputHandle), outMode);
        }

        if (_savedInputCodePage is { } inCp)
        {
            _ = SetConsoleCP(inCp);
        }

        if (_savedOutputCodePage is { } outCp)
        {
            _ = SetConsoleOutputCP(outCp);
        }
    }

    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const uint EnableProcessedInput = 0x0001;
    private const uint EnableLineInput = 0x0002;
    private const uint EnableEchoInput = 0x0004;
    private const uint EnableVirtualTerminalInput = 0x0200;
    private const uint EnableExtendedFlags = 0x0080;
    private const uint EnableQuickEditMode = 0x0040;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint DisableNewlineAutoReturn = 0x0008;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int GetConsoleMode(nint handle, out uint mode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int SetConsoleMode(nint handle, uint mode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetConsoleCP();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleCP(uint wCodePageID);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetConsoleOutputCP();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleOutputCP(uint wCodePageID);

    // ---- Unix ----------------------------------------------------------------------------

    private void EnableUnix()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return;
        }

        _savedStty = RunSttyCapture("-g");
        if (_savedStty is not null)
        {
            RunStty("raw -echo");
        }
    }

    // Run `stty <args> < /dev/tty` via the shell so it targets the controlling terminal.
    private static string? RunSttyCapture(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add($"stty {args} < /dev/tty");
            using Process? p = Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static void RunStty(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add($"stty {args} < /dev/tty");
            using Process? p = Process.Start(psi);
            p?.WaitForExit();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            // best-effort
        }
    }
}
