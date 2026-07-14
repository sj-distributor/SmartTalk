namespace SmartTalk.Core.Services.RealtimeAiV2.Recording;

/// <summary>
/// In-memory recording buffer that grows unbounded for the lifetime of the
/// session. Behaviourally identical to the previous direct
/// <c>MemoryStream</c> + <c>SemaphoreSlim</c> usage in
/// <c>RealtimeAiSessionContext</c> — extracted as a class to (a) encapsulate
/// the lock/stream pair, and (b) sit behind the same
/// <see cref="IRecordingBuffer"/> interface as the rolling-window
/// implementation introduced in PR 3.2.
///
/// <para>This is the default mode for SmartTalk today — no behaviour change.</para>
/// </summary>
public sealed class UnboundedMemoryBuffer : IRecordingBuffer
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private MemoryStream _stream = new();
    private bool _extracted;
    private bool _disposed;

    public async Task WriteAsync(ReadOnlyMemory<byte> data)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_extracted) return;

            await _stream.WriteAsync(data).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<byte[]> SnapshotAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_extracted || _stream.Length == 0) return [];

            var snapshot = new byte[_stream.Length];

            _stream.Position = 0;
            _ = await _stream.ReadAsync(snapshot.AsMemory()).ConfigureAwait(false);
            _stream.Position = _stream.Length;

            return snapshot;
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
            if (_extracted || _stream.Length == 0)
            {
                _extracted = true;
                return [];
            }

            var bytes = _stream.ToArray();

            await _stream.DisposeAsync().ConfigureAwait(false);
            _extracted = true;

            return bytes;
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

            if (!_extracted)
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
                _extracted = true;
            }

            _disposed = true;
        }
        finally
        {
            _lock.Release();
        }

        // Deliberately do NOT call `_lock.Dispose()`.
        //
        // SemaphoreSlim.Dispose is not thread-safe (per Microsoft docs). A late audio
        // frame arriving from the read loop while session cleanup is in flight can
        // capture a reference to this buffer before `_ctx.AudioBuffer` is nulled, then
        // call WriteAsync after Dispose has finished. With the lock disposed, that
        // call's `_lock.WaitAsync()` would throw ObjectDisposedException — a behaviour
        // change vs. the pre-refactor code, which never disposed any of the session's
        // SemaphoreSlim instances (BufferLock, WsSendLock, ProviderResponseStateLock).
        // Letting the GC reclaim the lock when the session scope dies preserves the
        // original "late writes are silent no-ops" contract: post-dispose WriteAsync
        // acquires the lock, sees `_extracted == true`, returns. Same for SnapshotAsync.
    }
}
