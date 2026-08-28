using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;
using Serilog;
using Serilog.Events;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private async Task OrchestrateSessionAsync()
    {
        var clientIsClose = false;
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            while (_ctx.WebSocket.State == WebSocketState.Open)
            {
                (var message, clientIsClose) = await ReadClientMessageAsync(buffer).ConfigureAwait(false);

                if (clientIsClose) break;

                if (message != null) await ProcessClientMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (WebSocketException ex)
        {
            Log.Error(ex, "[RealtimeAi] WebSocket error, SessionId: {SessionId}, WebSocketState: {WebSocketState}", _ctx.SessionId, GetWebSocketStateSafe());
        }
        catch (OperationCanceledException)
        {
            Log.Warning("[RealtimeAi] WebSocket cancelled, SessionId: {SessionId}", _ctx.SessionId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            
            await CleanupSessionAsync(clientIsClose).ConfigureAwait(false);
        }
    }

    private async Task<(string Message, bool ClientIsClose)> ReadClientMessageAsync(byte[] buffer)
    {
        var result = await _ctx.WebSocket.ReceiveAsync(buffer.AsMemory(), _ctx.SessionCts.Token).ConfigureAwait(false);

        if (result.MessageType == WebSocketMessageType.Close) return (null, true);

        // Whole-frame is the overwhelming majority: a Twilio media frame is around a kilobyte against
        // a pooled buffer many times that, arriving fifty times a second for the length of the call.
        // Decoding straight out of the pooled buffer drops a MemoryStream and its doubling regrowth
        // per frame; the reassembly below is unchanged for the messages that actually need it.
        if (result.EndOfMessage) return (Encoding.UTF8.GetString(buffer, 0, result.Count), false);

        return await ReassembleClientMessageAsync(buffer, result).ConfigureAwait(false);
    }

    private async Task<(string Message, bool ClientIsClose)> ReassembleClientMessageAsync(byte[] buffer, ValueWebSocketReceiveResult firstFrame)
    {
        using var ms = new MemoryStream();

        var result = firstFrame;
        ms.Write(buffer, 0, result.Count);

        while (!result.EndOfMessage)
        {
            result = await _ctx.WebSocket.ReceiveAsync(buffer.AsMemory(), _ctx.SessionCts.Token).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close) return (null, true);

            ms.Write(buffer, 0, result.Count);
        }

        return (Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length), false);
    }

    private async Task ProcessClientMessageAsync(string rawMessage)
    {
        try
        {
            var parsed = _ctx.ClientAdapter.ParseMessage(rawMessage);

            // Advance the stream clock for every frame that carries a timestamp, including
            // those we don't dispatch (e.g. video).
            if (parsed.Timestamp.HasValue)
                _ctx.LatestMediaTimestamp = parsed.Timestamp.Value;

            switch (parsed.Type)
            {
                case RealtimeAiClientMessageType.Start:
                    await (_ctx.Options.OnClientStartAsync?.Invoke(_ctx.SessionId, parsed.Metadata ?? new()) ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Stop:
                    await (_ctx.Options.OnClientStopAsync?.Invoke(_ctx.SessionId) ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Audio:
                    await HandleClientAudioAsync(parsed.Payload).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Image:
                    await HandleClientImageAsync(parsed.Payload).ConfigureAwait(false);
                    break;
                case RealtimeAiClientMessageType.Text:
                    await HandleClientTextAsync(parsed.Payload).ConfigureAwait(false);
                    break;
                default:
                    Log.Warning("[RealtimeAi] Unknown client message type, SessionId: {SessionId}", _ctx.SessionId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Failed to process client message, SessionId: {SessionId}", _ctx.SessionId);
        }
    }

    private async Task HandleClientAudioAsync(string base64Payload)
    {
        string providerBase64;

        try
        {
            providerBase64 = await TranscodeAudioAsync(base64Payload, AudioSource.Client).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A codec or sample-rate mismatch throws on every frame and garbles the whole call. Named
            // so the operator sees which stage failed, not just that handling a message did.
            Log.Error(ex, "[RealtimeAi] Audio transcode failed, SessionId: {SessionId}, Direction: {Direction}", _ctx.SessionId, nameof(AudioSource.Client));
            return;
        }

        if (_ctx.IsClientAudioToProviderSuspended) return;

        await SendAudioToProviderAsync(providerBase64).ConfigureAwait(false);
    }

    private async Task HandleClientImageAsync(string base64Payload)
    {
        await SendImageToProviderAsync(base64Payload).ConfigureAwait(false);
    }

    private async Task HandleClientTextAsync(string text)
    {
        await SendTextToProviderAsync(text).ConfigureAwait(false);
    }

    private async Task CleanupSessionAsync(bool clientIsClose)
    {
        // Reachable from the orchestration loop's finally and from ConnectAsync's, so the two cannot
        // both run it — a session must not flush its transcript or notify its consumer twice.
        if (Interlocked.Exchange(ref _ctx.CleanupClaimed, 1) == 1) return;

        var outcome = ResolveSessionOutcome(clientIsClose);

        if (clientIsClose)
            await SafeExecuteAsync(
                () => _ctx.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close acknowledged", CancellationToken.None), RealtimeAiCleanupStep.AcknowledgeClientClose);
        else
        {
            if (_ctx.Options.MaxSessionDuration.HasValue)
                await SafeExecuteAsync(
                    () => CloseClientWebSocketIfOpenAsync("Session ended"), RealtimeAiCleanupStep.CloseClientSocket);
        }
        
        await SafeExecuteAsync(
            () => DisconnectFromProviderAsync(clientIsClose ? "Client disconnected" : "Client disconnected abnormally"), RealtimeAiCleanupStep.DisconnectProvider);

        await SafeExecuteAsync(
            () => { _inactivityTimerManager.StopTimer(_ctx.SessionId); return Task.CompletedTask; }, RealtimeAiCleanupStep.StopIdleTimer);

        await SafeExecuteAsync(
            () => _ctx.Options?.OnSessionEndedAsync?.Invoke(_ctx.SessionId) ?? Task.CompletedTask, RealtimeAiCleanupStep.InvokeSessionEnded);

        await SafeExecuteAsync(HandleRecordingAsync, RealtimeAiCleanupStep.HandleRecording);
        await SafeExecuteAsync(HandleTranscriptionsAsync, RealtimeAiCleanupStep.HandleTranscriptions);

        LogSessionEnded(outcome);
    }

    /// <summary>
    /// A deliberate teardown records its own cause; anything else is read off the client socket. The
    /// duration ceiling is inferred rather than signalled because CancelAfter shares the session CTS
    /// with every other cancellation path, so there is nothing to distinguish it at the callback.
    /// </summary>
    private RealtimeAiSessionOutcome ResolveSessionOutcome(bool clientIsClose)
    {
        if (_ctx.TerminationCause is { } cause) return cause;

        if (clientIsClose) return RealtimeAiSessionOutcome.ClientClosed;

        if (_ctx.Options.MaxSessionDuration is { } ceiling && Stopwatch.GetElapsedTime(_ctx.SessionStartedAt) >= ceiling)
            return RealtimeAiSessionOutcome.MaxDurationReached;

        return RealtimeAiSessionOutcome.ClientAborted;
    }

    /// <summary>
    /// One line per session carrying how it ended and what it produced, levelled by outcome so
    /// "abnormal disconnect rate" becomes a number worth alerting on. Replaces two Warnings that
    /// attributed every ending — including the engine's own decisions — to the client.
    /// </summary>
    private void LogSessionEnded(RealtimeAiSessionOutcome outcome)
    {
        var level = outcome switch
        {
            RealtimeAiSessionOutcome.ProviderFault => LogEventLevel.Error,
            RealtimeAiSessionOutcome.ClientAborted => LogEventLevel.Warning,
            _ => LogEventLevel.Information
        };

        Log.Write(level,
            "[RealtimeAi] Session ended, SessionId: {SessionId}, Outcome: {Outcome}, ElapsedSessionMs: {ElapsedSessionMs}, " +
            "TurnCount: {TurnCount}, TranscriptionCount: {TranscriptionCount}, WebSocketState: {WebSocketState}",
            _ctx.SessionId, outcome, (long)Stopwatch.GetElapsedTime(_ctx.SessionStartedAt).TotalMilliseconds,
            _ctx.Round, _ctx.Transcriptions.Count, GetWebSocketStateSafe());
    }

    private async Task CloseClientWebSocketIfOpenAsync(string reason)
    {
        if (_ctx.WebSocket == null) return;

        if (_ctx.WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await _ctx.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None).ConfigureAwait(false);
    }

    private enum AudioSource { Client, Provider }

    /// <summary>
    /// Central audio pipeline: decode → record → convert codec → re-encode.
    /// Resolves source/target codecs from AudioSource; decodes at most once.
    /// </summary>
    private async Task<string> TranscodeAudioAsync(string base64Input, AudioSource source)
    {
        var clientCodec = _ctx.ClientAdapter.NativeAudioCodec;
        var providerCodec = source == AudioSource.Provider
            ? _ctx.TtsProvider.OutputCodec
            : _ctx.ProviderAdapter.GetPreferredCodec(clientCodec);
        var (sourceCodec, targetCodec) = source == AudioSource.Client ? (clientCodec, providerCodec) : (providerCodec, clientCodec);
        var sourceSampleRate = ResolveAudioSampleRate(sourceCodec, source);
        var targetSampleRate = ResolveAudioSampleRate(targetCodec, source == AudioSource.Client ? AudioSource.Provider : AudioSource.Client);

        var rawBytes = await RecordAudioIfRequiredAsync(base64Input, sourceCodec, source, sourceSampleRate).ConfigureAwait(false);

        if (sourceCodec == targetCodec && sourceSampleRate == targetSampleRate) return base64Input;

        rawBytes ??= Convert.FromBase64String(base64Input);

        var pcm16 = sourceCodec == RealtimeAiAudioCodec.PCM16
            ? rawBytes
            : AudioCodecConverter.Convert(rawBytes, sourceCodec, RealtimeAiAudioCodec.PCM16);

        if (sourceSampleRate != targetSampleRate)
            pcm16 = AudioCodecConverter.Resample(pcm16, sourceSampleRate, targetSampleRate);

        var outputBytes = targetCodec == RealtimeAiAudioCodec.PCM16
            ? pcm16
            : AudioCodecConverter.Convert(pcm16, RealtimeAiAudioCodec.PCM16, targetCodec);

        return Convert.ToBase64String(outputBytes);
    }

    private int ResolveAudioSampleRate(RealtimeAiAudioCodec codec, AudioSource source)
    {
        if (source == AudioSource.Provider)
            return _ctx.TtsProvider.OutputSampleRate;

        return AudioCodecConverter.GetSampleRate(codec);
    }

    /// <summary>
    /// Decides whether recording should happen, decodes and writes to buffer if so.
    /// Returns decoded bytes for reuse by codec conversion, or null if no decode occurred.
    /// </summary>
    private async Task<byte[]> RecordAudioIfRequiredAsync(string base64Input, RealtimeAiAudioCodec sourceCodec, AudioSource source, int sourceSampleRate)
    {
        if (!_ctx.Options.EnableRecording) return null;
        if (source == AudioSource.Client && _ctx.IsAiSpeaking) return null;

        var rawBytes = Convert.FromBase64String(base64Input);
        
        await WriteToAudioBufferAsync(rawBytes, sourceCodec, sourceSampleRate).ConfigureAwait(false);
        
        return rawBytes;
    }

    private async Task WriteToAudioBufferAsync(byte[] data, RealtimeAiAudioCodec sourceCodec, int sourceSampleRate)
    {
        if (!_ctx.Options.EnableRecording) return;

        // Capture the buffer reference ONCE. `_ctx.AudioBuffer` may be nulled by
        // HandleRecordingAsync between the null check and the WriteAsync call —
        // a real race when a late audio frame arrives during cleanup. Reading the
        // field twice (`is null` check + `.WriteAsync`) would NRE on null. The
        // captured reference is still valid even after `_ctx.AudioBuffer = null`,
        // and the buffer's internal `_extracted` flag turns the late write into
        // a silent no-op (matching the pre-refactor BufferLock + double-check pattern).
        var buffer = _ctx.AudioBuffer;
        if (buffer is null) return;

        var pcmData = AudioCodecConverter.ConvertForRecording(data, sourceCodec, sourceSampleRate);

        await buffer.WriteAsync(pcmData).ConfigureAwait(false);
    }

    private async Task HandleTranscriptionsAsync()
    {
        if (_ctx.Options.OnTranscriptionsCompletedAsync == null) return;

        if (_ctx.Transcriptions.IsEmpty)
        {
            // A finished call that transcribed nothing is the exact shape of the calls that silently
            // never reach the database, so it is worth surfacing rather than returning quietly.
            Log.Warning("[RealtimeAi] Session ended with no transcriptions, SessionId: {SessionId}", _ctx.SessionId);
            return;
        }

        var transcriptions = _ctx.Transcriptions.Select(t => (t.Speaker, t.Text)).ToList();
        
        await _ctx.Options.OnTranscriptionsCompletedAsync(_ctx.SessionId, transcriptions).ConfigureAwait(false);
    }
    
    private async Task<byte[]> GetRecordedAudioSnapshotAsync()
    {
        if (!_ctx.Options.EnableRecording) return [];

        // Capture the buffer reference ONCE — same race rationale as
        // WriteToAudioBufferAsync above. RepeatOrder calls this from a function-call
        // handler, which can fire concurrently with cleanup.
        var buffer = _ctx.AudioBuffer;
        if (buffer is null) return [];

        var snapshotBytes = await buffer.SnapshotAsync().ConfigureAwait(false);

        if (snapshotBytes.Length == 0) return [];

        var waveFormat = new WaveFormat(24000, 16, 1);
        using var wavStream = new MemoryStream();
        await using (var writer = new WaveFileWriter(wavStream, waveFormat))
        {
            writer.Write(snapshotBytes, 0, snapshotBytes.Length);
            await writer.FlushAsync();
        }

        return wavStream.ToArray();
    }

    private async Task HandleRecordingAsync()
    {
        if (!_ctx.Options.EnableRecording) return;
        if (_ctx.AudioBuffer is null) return;

        var buffer = _ctx.AudioBuffer;
        _ctx.AudioBuffer = null;

        try
        {
            // Extraction and encoding produce a byte[] the size of the whole call plus a second copy
            // behind WaveFileWriter. With no consumer for the result that is pure garbage, so skip
            // it — but skip it from INSIDE the try, or the buffer never gets disposed either.
            if (_ctx.Options.OnRecordingCompleteAsync == null) return;

            var pcmBytes = await buffer.ExtractAsync().ConfigureAwait(false);

            if (pcmBytes.Length == 0)
            {
                Log.Warning("[RealtimeAi] Recording was enabled but no audio was recorded, SessionId: {SessionId}", _ctx.SessionId);
                return;
            }

            var waveFormat = new WaveFormat(24000, 16, 1);
            using var wavStream = new MemoryStream();

            await using (var writer = new WaveFileWriter(wavStream, waveFormat))
            {
                writer.Write(pcmBytes, 0, pcmBytes.Length);
                await writer.FlushAsync();
            }

            await _ctx.Options.OnRecordingCompleteAsync(_ctx.SessionId, wavStream.ToArray()).ConfigureAwait(false);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }
    
    private async Task SafeExecuteAsync(Func<Task> action, RealtimeAiCleanupStep step)
    {
        try
        {
            await action().ConfigureAwait(false);

            Log.Debug("[RealtimeAi] Cleanup step done, SessionId: {SessionId}, CleanupStep: {CleanupStep}", _ctx.SessionId, step);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Cleanup step failed, SessionId: {SessionId}, CleanupStep: {CleanupStep}", _ctx.SessionId, step);
        }
    }
}
