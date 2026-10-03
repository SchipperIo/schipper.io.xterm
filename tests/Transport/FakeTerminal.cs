using Schipper.Io.Xterm.Transport;

namespace Schipper.Io.Xterm.Tests.Transport;

internal sealed class FakeTerminal : ITerminal
{
    private readonly Queue<byte[]> _chunks = new();
    private readonly SemaphoreSlim _available = new(0);
    private byte[]? _current;
    private int _offset;

    public FakeTerminal(int columns = 80, int rows = 24)
    {
        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; set; }

    public int Rows { get; set; }

    public List<byte> Written { get; } = new();

    public event Action? Resized
    {
        add { }
        remove { }
    }

    public void Enqueue(params byte[] bytes)
    {
        _chunks.Enqueue(bytes);
        _ = _available.Release();
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        Written.AddRange(bytes.ToArray());
        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_current is null || _offset >= _current.Length)
        {
            await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_chunks.Count == 0)
            {
                return 0;
            }

            _current = _chunks.Dequeue();
            _offset = 0;
        }

        int n = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsSpan(_offset, n).CopyTo(buffer.Span);
        _offset += n;
        return n;
    }
}
