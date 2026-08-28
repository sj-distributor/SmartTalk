using System.Diagnostics;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    /// <summary>
    /// Test-only seam (internal, not an operator surface): supplies the recording buffer so a test can
    /// observe extraction and disposal. Null in production — RealtimeAiRecordingSettings decides.
    /// </summary>
    internal Func<IRecordingBuffer> RecordingBufferFactoryOverride { get; set; }

    private void BuildSessionContext(RealtimeSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.ClientConfig);
        ArgumentNullException.ThrowIfNull(options.ModelConfig);
        ArgumentNullException.ThrowIfNull(options.ModelConfig.ServiceUrl);
        ArgumentNullException.ThrowIfNull(options.ConnectionProfile);

        _ctx = new RealtimeAiSessionContext
        {
            // A consumer-supplied id lets its own pre-connect lines share the engine's correlation
            // value; blank or absent falls back to the property's own generated default.
            SessionId = string.IsNullOrWhiteSpace(options.SessionId) ? Guid.NewGuid().ToString() : options.SessionId,
            SessionStartedAt = Stopwatch.GetTimestamp(),
            Options = options,
            WebSocket = options.WebSocket,
            SessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };

        BuildConnectSwitcher();
        BuildRecordingIfRequired();
        BuildSessionActions();
        ApplyMaxSessionDurationIfRequired();
    }

    /// <summary>
    /// Records the cause before cancelling, rather than leaving teardown to infer it.
    ///
    /// <para>This used to be <c>SessionCts.CancelAfter(ceiling)</c>, with ResolveSessionOutcome
    /// deciding afterwards by comparing <c>Stopwatch.GetElapsedTime(SessionStartedAt)</c> against the
    /// ceiling. Those are two different clocks: the cancellation runs off the timer queue, which can
    /// fire a tick before Stopwatch agrees the ceiling has elapsed, and the comparison then falls
    /// through to ClientAborted. Around 4% of ceiling-terminated sessions were reported as the caller
    /// hanging up — the exact misattribution the outcome property was introduced to remove, and the
    /// cause of an intermittent red that had been widened twice as if it were a slow test
    /// (RealtimeAiServiceDurationCeilingStressTests).</para>
    ///
    /// <para>The delay is cancelled by the session token, so a session that ends for any other reason
    /// never runs the continuation and nothing is left scheduled. ResolveSessionOutcome's own
    /// inference is left in place untouched, below the TerminationCause check, as a fallback.</para>
    /// </summary>
    private void ApplyMaxSessionDurationIfRequired()
    {
        if (_ctx.Options.MaxSessionDuration is not { } maxSessionDuration || maxSessionDuration <= TimeSpan.Zero)
            return;

        var sessionCts = _ctx.SessionCts;

        _ = Task.Delay(maxSessionDuration, sessionCts.Token).ContinueWith(_ => CancelAtCeiling(sessionCts), TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private void CancelAtCeiling(CancellationTokenSource sessionCts)
    {
        _ctx.TerminationCause ??= RealtimeAiSessionOutcome.MaxDurationReached;

        try
        {
            sessionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Teardown disposed the source between the ceiling elapsing and this call. The session is
            // already ending, which is what the cancel was for.
        }
    }

    private void BuildRecordingIfRequired()
    {
        // RealtimeAiRecordingSettings.Create() picks UnboundedMemoryBuffer (default) or
        // RollingWindowBuffer based on the BufferMode env var. Default preserves the
        // pre-Phase-3 unbounded behaviour exactly.
        if (_ctx.Options.EnableRecording && _ctx.AudioBuffer == null) _ctx.AudioBuffer = RecordingBufferFactoryOverride?.Invoke() ?? RealtimeAiRecordingSettings.Create();
    }

    private void BuildSessionActions()
    {
        _ctx.SessionActions = new RealtimeAiSessionActions
        {
            SendAudioToClientAsync = SendAudioToClientAsync,
            SendTextToProviderAsync = SendTextToProviderAsync,
            SuspendClientAudioToProvider = () => _ctx.IsClientAudioToProviderSuspended = true,
            ResumeClientAudioToProvider = () => _ctx.IsClientAudioToProviderSuspended = false,
            GetRecordedAudioSnapshotAsync = GetRecordedAudioSnapshotAsync
        };
    }

    private void BuildConnectSwitcher()
    {
        _ctx.WssClient = _realtimeAiSwitcher.WssClient(_ctx.Options.ModelConfig.Provider);
        _ctx.ClientAdapter = _realtimeAiSwitcher.ClientAdapter(_ctx.Options.ClientConfig.Client);
        _ctx.ProviderAdapter = _realtimeAiSwitcher.ProviderAdapter(_ctx.Options.ModelConfig.Provider);
        _ctx.TtsProvider = _realtimeAiSwitcher.TtsProvider(_ctx.Options.TtsConfig?.ProviderType ?? RealtimeAiTtsProviderType.BuiltIn);
    }
}
