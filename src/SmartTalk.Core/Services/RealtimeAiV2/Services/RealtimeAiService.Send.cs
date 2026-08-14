using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    // ── Low-level ───────────────────────────────────────────────

    private async Task SendToClientAsync(object payload)
    {
        // null payload signals the client adapter chose to drop the frame
        // (e.g. Twilio adapter when streamSid is not yet known). Skip the send.
        if (payload is null) return;

        if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

        await _ctx.WsSendLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            await _ctx.WebSocket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, _ctx.SessionCts?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "[RealtimeAi] Failed to send to client, SessionId: {SessionId}, WebSocketState: {WebSocketState}", _ctx.SessionId, _ctx.WebSocket?.State);
        }
        finally
        {
            _ctx.WsSendLock.Release();
        }
    }

    private async Task SendToProviderAsync(params string[] messages)
    {
        if (!IsProviderSessionActive) return;

        foreach (var message in messages)
        {
            if (message != null)
                await _ctx.WssClient.SendMessageAsync(message, _ctx.SessionCts.Token).ConfigureAwait(false);
        }
    }

    // ── High-level ────────────────────────────────────────────────

    private async Task SendAudioToClientAsync(string base64Payload)
    {
        await SendAudioToClientAsync(
            base64Payload,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            providerSeq: null,
            audioReadyDelayMs: 0,
            transcodeDurationMs: null).ConfigureAwait(false);
    }

    private async Task SendAudioToClientAsync(
        string base64Payload,
        long serverReceivedAtUnixMs,
        long? providerSeq,
        double audioReadyDelayMs,
        double? transcodeDurationMs)
    {
        if (string.IsNullOrEmpty(base64Payload)) return;
        if (_ctx.WebSocket is not { State: WebSocketState.Open }) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var sendLockStopwatch = Stopwatch.StartNew();
        await _ctx.WsSendLock.WaitAsync(token).ConfigureAwait(false);
        sendLockStopwatch.Stop();

        var seq = Interlocked.Increment(ref _ctx.OutboundAudioSequence);
        var serverSendStartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var metadata = new RealtimeAiAudioDeliveryMetadata
        {
            Seq = seq,
            ProviderSeq = providerSeq,
            ServerReceivedAtUnixMs = serverReceivedAtUnixMs,
            ServerSendStartedAtUnixMs = serverSendStartedAtUnixMs,
            AudioReadyDelayMs = audioReadyDelayMs,
            TranscodeDurationMs = transcodeDurationMs,
            SendLockWaitMs = sendLockStopwatch.Elapsed.TotalMilliseconds
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var deliveryDiagnostics = _ctx.ClientAdapter as IRealtimeAiAudioDeliveryDiagnostics;
            var payload = deliveryDiagnostics != null
                ? deliveryDiagnostics.BuildAudioDeltaMessage(base64Payload, _ctx.SessionId, metadata)
                : _ctx.ClientAdapter.BuildAudioDeltaMessage(base64Payload, _ctx.SessionId);

            if (payload == null) return;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            await _ctx.WebSocket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, token).ConfigureAwait(false);

            stopwatch.Stop();
            var serverSendCompletedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var audioBytes = EstimateBase64DecodedLength(base64Payload);
            var audioDurationMs = EstimateAudioDurationMs(audioBytes, _ctx.ClientAdapter.NativeAudioCodec);

            if (deliveryDiagnostics != null)
                Log.Information(
                    "[RealtimeAi][AudioDelivery] Sent audio chunk, SessionId: {SessionId}, Seq: {Seq}, ProviderSeq: {ProviderSeq}, PayloadBytes: {PayloadBytes}, WireBytes: {WireBytes}, AudioDurationMs: {AudioDurationMs}, ServerReceivedAtUnixMs: {ServerReceivedAtUnixMs}, ServerSendStartedAtUnixMs: {ServerSendStartedAtUnixMs}, ServerSendCompletedAtUnixMs: {ServerSendCompletedAtUnixMs}, AudioReadyDelayMs: {AudioReadyDelayMs}, TranscodeDurationMs: {TranscodeDurationMs}, SendLockWaitMs: {SendLockWaitMs}, InternalDelayMs: {InternalDelayMs}, SendDurationMs: {SendDurationMs}",
                    _ctx.SessionId, seq, providerSeq, audioBytes, bytes.Length, audioDurationMs, serverReceivedAtUnixMs, serverSendStartedAtUnixMs,
                    serverSendCompletedAtUnixMs, audioReadyDelayMs, transcodeDurationMs, sendLockStopwatch.Elapsed.TotalMilliseconds,
                    serverSendStartedAtUnixMs - serverReceivedAtUnixMs, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Log.Information(
                "[RealtimeAi][AudioDelivery] Audio send cancelled, SessionId: {SessionId}, Seq: {Seq}, ProviderSeq: {ProviderSeq}, SendLockWaitMs: {SendLockWaitMs}, SendDurationMs: {SendDurationMs}, WebSocketState: {WebSocketState}",
                _ctx.SessionId, seq, providerSeq, sendLockStopwatch.Elapsed.TotalMilliseconds,
                stopwatch.Elapsed.TotalMilliseconds, _ctx.WebSocket?.State);
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex,
                "[RealtimeAi][AudioDelivery] Failed to send audio chunk, SessionId: {SessionId}, Seq: {Seq}, ProviderSeq: {ProviderSeq}, SendLockWaitMs: {SendLockWaitMs}, SendDurationMs: {SendDurationMs}, WebSocketState: {WebSocketState}",
                _ctx.SessionId, seq, providerSeq, sendLockStopwatch.Elapsed.TotalMilliseconds,
                stopwatch.Elapsed.TotalMilliseconds, _ctx.WebSocket?.State);
        }
        finally
        {
            _ctx.WsSendLock.Release();
        }
    }

    private static int EstimateBase64DecodedLength(string base64Payload)
    {
        var padding = base64Payload.EndsWith("==", StringComparison.Ordinal) ? 2
            : base64Payload.EndsWith("=", StringComparison.Ordinal) ? 1
            : 0;

        return base64Payload.Length * 3 / 4 - padding;
    }

    private static double EstimateAudioDurationMs(int audioBytes, RealtimeAiAudioCodec codec)
    {
        var bytesPerSample = codec == RealtimeAiAudioCodec.PCM16 ? 2 : 1;
        return audioBytes * 1000d / (AudioCodecConverter.GetSampleRate(codec) * bytesPerSample);
    }

    private async Task SendAudioToProviderAsync(string base64Payload)
    {
        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(new RealtimeAiWssAudioData { Base64Payload = base64Payload })).ConfigureAwait(false);
    }

    private async Task SendImageToProviderAsync(string base64Payload)
    {
        await SendToProviderAsync(_ctx.ProviderAdapter.BuildAudioAppendMessage(new RealtimeAiWssAudioData
        {
            Base64Payload = base64Payload,
            CustomProperties = new Dictionary<string, object> { { "image", base64Payload } }
        })).ConfigureAwait(false);
    }

    private async Task SendTextToProviderAsync(string text)
    {
        Log.Information("[RealtimeAi] Sending text to provider, SessionId: {SessionId}, Text: {Text}", _ctx.SessionId, text);

        await SendToProviderAsync(_ctx.ProviderAdapter.BuildTextUserMessage(text, _ctx.SessionId)).ConfigureAwait(false);
        await QueueOrTriggerProviderResponseAsync("text input").ConfigureAwait(false);
    }

    private async Task QueueOrTriggerProviderResponseAsync(string source)
    {
        if (!IsProviderSessionActive) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldSendTrigger = false;

        await _ctx.ProviderResponseStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_ctx.IsProviderResponseInProgress)
            {
                _ctx.HasPendingProviderResponseTrigger = true;
                Log.Information("[RealtimeAi] Response trigger queued, SessionId: {SessionId}, Source: {Source}", _ctx.SessionId, source);
                return;
            }
            
            _ctx.HasPendingProviderResponseTrigger = false;
            _ctx.IsProviderResponseInProgress = true;
            shouldSendTrigger = true;
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }

        if (!shouldSendTrigger) return;

        try
        {
            await SendToProviderAsync(_ctx.ProviderAdapter.BuildTriggerResponseMessage()).ConfigureAwait(false);
        }
        catch
        {
            await _ctx.ProviderResponseStateLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _ctx.IsProviderResponseInProgress = false;
                _ctx.HasPendingProviderResponseTrigger = true;
            }
            finally
            {
                _ctx.ProviderResponseStateLock.Release();
            }

            throw;
        }
    }

    private async Task MarkProviderResponseStartedAsync()
    {
        if (!IsProviderSessionActive) return;

        await _ctx.ProviderResponseStateLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            _ctx.IsProviderResponseInProgress = true;
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }
    }

    private async Task MarkProviderResponseCompletedAndDrainAsync()
    {
        if (!IsProviderSessionActive) return;

        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;
        var shouldSendQueuedTrigger = false;

        await _ctx.ProviderResponseStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _ctx.IsProviderResponseInProgress = false;

            if (_ctx.HasPendingProviderResponseTrigger)
            {
                _ctx.HasPendingProviderResponseTrigger = false;
                _ctx.IsProviderResponseInProgress = true;
                shouldSendQueuedTrigger = true;
            }
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }

        if (shouldSendQueuedTrigger)
        {
            try
            {
                await SendToProviderAsync(_ctx.ProviderAdapter.BuildTriggerResponseMessage()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _ctx.ProviderResponseStateLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    _ctx.IsProviderResponseInProgress = false;
                    _ctx.HasPendingProviderResponseTrigger = true;
                }
                finally
                {
                    _ctx.ProviderResponseStateLock.Release();
                }

                Log.Warning(ex, "[RealtimeAi] Failed to send queued response trigger, SessionId: {SessionId}", _ctx.SessionId);
            }
        }
    }
}
