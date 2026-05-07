namespace SmartTalk.Core.Services.RealtimeAiV2.Recording;

/// <summary>
/// Fixed-capacity recording buffer that drops the oldest PCM bytes when the
/// cap is reached. Internally a circular byte array — O(1) writes and snapshots.
///
/// <para>Sized to comfortably exceed any realistic call duration so a normal
/// session behaves identically to <see cref="UnboundedMemoryBuffer"/>; the
/// cap only kicks in for pathologically long calls (the scenario that today
/// produces 86MB+ memory pressure per call).</para>
///
/// <para>Snapshot and Extract return bytes in chronological order regardless
/// of where the write head currently sits in the underlying array.</para>
/// </summary>
public sealed class RollingWindowBuffer : IRecordingBuffer
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly byte[] _buffer;
    private readonly int _capacity;

    private int _writePos;
    private bool _wrapped;
    private bool _extracted;
    private bool _disposed;

    public RollingWindowBuffer(int capacityBytes)
    {
        if (capacityBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes), "Capacity must be positive.");

        _capacity = capacityBytes;
        _buffer = new byte[capacityBytes];
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_extracted) return;

            WriteUnderLock(data.Span);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Sync body of the write — span manipulation can't sit inside an async method
    /// (ReadOnlySpan is a ref struct; unsupported in async on C# 12). Caller holds the lock.
    /// </summary>
    private void WriteUnderLock(ReadOnlySpan<byte> span)
    {
        if (span.Length >= _capacity)
        {
            // Single write larger than capacity — keep only the trailing capacity bytes.
            span.Slice(span.Length - _capacity).CopyTo(_buffer);
            _writePos = 0;
            _wrapped = true;
            return;
        }

        // First chunk: write up to the end of the underlying array.
        var firstChunkSize = Math.Min(span.Length, _capacity - _writePos);
        span.Slice(0, firstChunkSize).CopyTo(_buffer.AsSpan(_writePos));

        if (firstChunkSize == span.Length)
        {
            _writePos += firstChunkSize;
            if (_writePos == _capacity)
            {
                _writePos = 0;
                _wrapped = true;
            }
            return;
        }

        // Wrap: remaining bytes go to the start of the array.
        span.Slice(firstChunkSize).CopyTo(_buffer);
        _writePos = span.Length - firstChunkSize;
        _wrapped = true;
    }

    public async Task<byte[]> SnapshotAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _extracted ? [] : BuildSnapshot();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<byte[]> ExtractAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_extracted) return [];

            var snapshot = BuildSnapshot();
            _extracted = true;

            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;

            _extracted = true;
            _disposed = true;
        }
        finally
        {
            _lock.Release();
        }

        // Deliberately do NOT call `_lock.Dispose()` — same rationale as
        // UnboundedMemoryBuffer.DisposeAsync. SemaphoreSlim.Dispose is not
        // thread-safe; a late audio frame arriving after cleanup must be a
        // silent no-op (post-dispose Write/Snapshot/Extract acquire the
        // lock, see `_extracted == true`, return). Letting the GC reclaim
        // the lock when the session scope dies matches V2's existing
        // pattern for all other session-scoped semaphores.
    }

    /// <summary>Builds the chronologically-ordered snapshot. Caller must hold the lock.</summary>
    private byte[] BuildSnapshot()
    {
        if (!_wrapped)
        {
            if (_writePos == 0) return [];

            var linear = new byte[_writePos];
            _buffer.AsSpan(0, _writePos).CopyTo(linear);
            return linear;
        }

        // Wrapped: oldest bytes start at _writePos and run to the end of the array,
        // followed by [0, _writePos).
        var snapshot = new byte[_capacity];
        var tailSize = _capacity - _writePos;

        _buffer.AsSpan(_writePos, tailSize).CopyTo(snapshot);
        _buffer.AsSpan(0, _writePos).CopyTo(snapshot.AsSpan(tailSize));

        return snapshot;
    }
}
