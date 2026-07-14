namespace SmartTalk.Core.Services.RealtimeAiV2.Recording;

/// <summary>
/// Per-session PCM recording sink. Implementations are responsible for their
/// own thread safety — concurrent <see cref="WriteAsync"/> and
/// <see cref="SnapshotAsync"/> calls are expected (provider audio arrives on
/// one thread, the user-driven RepeatOrder snapshot can fire from another).
///
/// <para>Bytes appended via <see cref="WriteAsync"/> must already be raw PCM
/// in the format the consumer will wrap (today: 24kHz mono S16LE — the
/// <see cref="Adapters.AudioCodecConverter.ConvertForRecording"/> output).
/// The buffer does not interpret the bytes; WAV header wrapping happens at
/// the call site.</para>
/// </summary>
public interface IRecordingBuffer : IAsyncDisposable
{
    /// <summary>
    /// Append PCM bytes. Some implementations may transparently drop the
    /// oldest bytes once a size cap is reached (rolling-window mode).
    /// No-op after <see cref="ExtractAsync"/> has been called.
    /// </summary>
    Task WriteAsync(ReadOnlyMemory<byte> data);

    /// <summary>
    /// Returns a snapshot of the currently buffered PCM bytes. Non-destructive —
    /// subsequent writes continue to extend the buffer. Returns an empty array
    /// when the buffer is empty or already extracted.
    /// </summary>
    Task<byte[]> SnapshotAsync();

    /// <summary>
    /// Detaches the buffered PCM bytes for one-time consumption (the session-end
    /// recording finalization path). The buffer becomes unusable after this
    /// returns: subsequent writes are no-ops, subsequent snapshots return empty.
    /// </summary>
    Task<byte[]> ExtractAsync();
}
