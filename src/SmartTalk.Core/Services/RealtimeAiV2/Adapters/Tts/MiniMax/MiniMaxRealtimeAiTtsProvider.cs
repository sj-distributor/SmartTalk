using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts.MiniMax;

public class MiniMaxRealtimeAiTtsProvider : IRealtimeAiTtsProvider, IRealtimeAiTextSynthesizer
{
    private const string DefaultServiceUrl = "wss://api.minimax.io/ws/v1/t2a_v2";
    private const string DefaultModel = "speech-2.8-turbo";
    private const string DefaultVoiceId = "Chinese (Mandarin)_News_Anchor";
    private const int DefaultSampleRate = 8000;
    private const int DefaultBitrate = 128000;
    private const int MinStreamingSegmentChars = 10;
    private const int MaxLoggedTextChars = 500;

    private static readonly char[] SentenceBoundaries = ['.', '!', '?', ';', '\n', '。', '！', '？', '；'];

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly object _textBufferLock = new();
    private readonly object _pendingSegmentsLock = new();
    private readonly object _turnStateLock = new();
    private readonly StringBuilder _textBuffer = new();
    private readonly Queue<string> _pendingSegments = new();

    // Per-turn synthesis tracking. MiniMax finalizes each task_continue segment with its own
    // is_final, but the engine expects a single SynthesisCompleted per AI turn. We therefore
    // raise completion only once the provider text is done AND every accepted segment has been
    // finalized. Guarded by _turnStateLock.
    private int _outstandingSegments;
    private bool _turnTextDone;
    private bool _turnSynthesisRaised;

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _receiveLoopCts;
    private Task _receiveLoopTask;
    private RealtimeAiTtsConfig _config;
    private int _generation;
    private int _loggedAudioSampleRate;
    private int _targetSampleRate = DefaultSampleRate;
    private int _assumedSourceSampleRate = DefaultSampleRate;
    private int _loggedFallbackSampleRate;
    private int _loggedWavSampleRate;

    public RealtimeAiTtsProviderType TtsProviderType => RealtimeAiTtsProviderType.MiniMax;

    public RealtimeAiAudioCodec OutputCodec => RealtimeAiAudioCodec.PCM16;

    public int OutputSampleRate => _targetSampleRate;

    public event Func<string, Task> AudioChunkReadyAsync;

    public event Func<Task> SynthesisCompletedAsync;

    public event Func<RealtimeAiErrorData, Task> SynthesisFailedAsync;

    public async Task InitializeAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
    {
        _config = config;
        _targetSampleRate = config.SampleRate ?? DefaultSampleRate;
        _assumedSourceSampleRate = GetIntConfig(config.ProviderSpecificConfig, "source_sample_rate", _targetSampleRate);
        ClearTextState(clearPending: true);

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await OpenConnectionAsync(config, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task HandleProviderTextDeltaAsync(string textDelta, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(textDelta)) return;

        BeginTurnIfCompleted();

        List<string> textSegments;
        lock (_textBufferLock)
        {
            _textBuffer.Append(textDelta);
            textSegments = DrainCompletedSegmentsLocked(flushAll: false);
        }

        foreach (var segment in textSegments)
            await QueueOrSendSegmentAsync(segment, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleProviderTextDoneAsync(CancellationToken cancellationToken)
    {
        List<string> textSegments;
        lock (_textBufferLock)
        {
            textSegments = DrainCompletedSegmentsLocked(flushAll: true);
        }

        Log.Information("[RealtimeAi][MiniMaxTts] Text done flush, Segments: {Count}", textSegments.Count);

        foreach (var segment in textSegments)
            await QueueOrSendSegmentAsync(segment, cancellationToken).ConfigureAwait(false);

        await MarkTurnTextDoneAndCompleteIfReadyAsync().ConfigureAwait(false);
    }

    public async Task HandleInterruptAsync(CancellationToken cancellationToken)
    {
        ClearTextState(clearPending: true);
        Interlocked.Increment(ref _generation);

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Log.Information("[RealtimeAi][MiniMaxTts] Interrupt received, restarting TTS session.");
            await CloseConnectionAsync(CancellationToken.None).ConfigureAwait(false);
            await OpenConnectionAsync(_config ?? new RealtimeAiTtsConfig(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ClearTextState(clearPending: true);
        Interlocked.Increment(ref _generation);

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task OpenConnectionAsync(RealtimeAiTtsConfig config, CancellationToken cancellationToken)
    {
        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            ws.Options.SetRequestHeader("Authorization", $"Bearer {config.ApiKey}");

        var serviceUrl = string.IsNullOrWhiteSpace(config.ServiceUrl) ? DefaultServiceUrl : config.ServiceUrl;
        Log.Information("[RealtimeAi][MiniMaxTts] Connecting websocket, Url: {Url}", serviceUrl);

        await ws.ConnectAsync(new Uri(serviceUrl), cancellationToken).ConfigureAwait(false);
        await WaitForEventAsync(ws, "connected_success", cancellationToken).ConfigureAwait(false);
        await SendTaskStartAsync(ws, config, cancellationToken).ConfigureAwait(false);
        await WaitForEventAsync(ws, "task_started", cancellationToken).ConfigureAwait(false);

        var loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var generation = Interlocked.Increment(ref _generation);
        var loopTask = Task.Run(() => ReceiveLoopAsync(ws, generation, loopCts.Token), loopCts.Token);

        _webSocket = ws;
        _receiveLoopCts = loopCts;
        _receiveLoopTask = loopTask;

        await FlushPendingSegmentsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CloseConnectionAsync(CancellationToken cancellationToken)
    {
        var ws = _webSocket;
        var loopCts = _receiveLoopCts;
        var loopTask = _receiveLoopTask;

        _webSocket = null;
        _receiveLoopCts = null;
        _receiveLoopTask = null;

        if (ws == null)
        {
            loopCts?.Cancel();
            loopCts?.Dispose();
            return;
        }

        try
        {
            if (ws.State == WebSocketState.Open)
                await SendAsyncInternal(ws, new { @event = "task_finish" }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeAi][MiniMaxTts] Failed to send task_finish.");
        }

        try
        {
            loopCts?.Cancel();
        }
        catch
        {
            // Best-effort shutdown.
        }

        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "MiniMax TTS stop", CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort shutdown.
            }
        }

        if (loopTask != null)
            await Task.WhenAny(loopTask, Task.Delay(1000, CancellationToken.None)).ConfigureAwait(false);

        ws.Dispose();
        loopCts?.Dispose();
    }

    private async Task SendTaskStartAsync(ClientWebSocket ws, RealtimeAiTtsConfig config, CancellationToken cancellationToken)
    {
        var model = GetStringConfig(config.ProviderSpecificConfig, "model", DefaultModel);
        var voiceId = string.IsNullOrWhiteSpace(config.Voice) ? DefaultVoiceId : config.Voice;
        var speed = GetDoubleConfig(config.ProviderSpecificConfig, "speed", 1.0d);
        var volume = GetDoubleConfig(config.ProviderSpecificConfig, "vol", 1.0d);
        var pitch = GetIntConfig(config.ProviderSpecificConfig, "pitch", 0);
        var sampleRate = config.SampleRate ?? DefaultSampleRate;
        var bitrate = GetIntConfig(config.ProviderSpecificConfig, "bitrate", DefaultBitrate);

        _targetSampleRate = sampleRate;

        var payload = new
        {
            @event = "task_start",
            model,
            voice_setting = new
            {
                voice_id = voiceId,
                speed,
                vol = volume,
                pitch
            },
            audio_setting = new
            {
                sample_rate = sampleRate,
                bitrate,
                format = "pcm",
                channel = 1
            }
        };

        Log.Information(
            "[RealtimeAi][MiniMaxTts] Sending task_start, Model: {Model}, VoiceId: {VoiceId}, SampleRate: {SampleRate}",
            model,
            voiceId,
            sampleRate);

        await SendAsyncInternal(ws, payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task QueueOrSendSegmentAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Count the moment the segment is accepted for synthesis, whether it is sent now or
        // queued for a not-yet-ready socket, so the turn-completion check waits for its is_final.
        lock (_turnStateLock)
        {
            _outstandingSegments++;
        }

        if (await SendTaskContinueAsync(text, cancellationToken).ConfigureAwait(false))
            return;

        lock (_pendingSegmentsLock)
        {
            _pendingSegments.Enqueue(text);
        }

        Log.Information(
            "[RealtimeAi][MiniMaxTts] task_continue queued (ws not ready), TextLength: {Length}, Text: {Text}",
            text.Length,
            BuildLogTextPreview(text));
    }

    private async Task FlushPendingSegmentsAsync(CancellationToken cancellationToken)
    {
        List<string> pending;
        lock (_pendingSegmentsLock)
        {
            if (_pendingSegments.Count == 0) return;
            pending = _pendingSegments.ToList();
            _pendingSegments.Clear();
        }

        for (var i = 0; i < pending.Count; i++)
        {
            if (await SendTaskContinueAsync(pending[i], cancellationToken).ConfigureAwait(false))
                continue;

            lock (_pendingSegmentsLock)
            {
                for (var j = i; j < pending.Count; j++)
                    _pendingSegments.Enqueue(pending[j]);
            }

            return;
        }
    }

    private async Task<bool> SendTaskContinueAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (_webSocket is not { State: WebSocketState.Open } ws) return false;

        Log.Information(
            "[RealtimeAi][MiniMaxTts] Sending task_continue, TextLength: {Length}, Text: {Text}",
            text.Length,
            BuildLogTextPreview(text));

        return await SendAsyncInternal(ws, new
        {
            @event = "task_continue",
            text
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildLogTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var normalized = text.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\r");
        return normalized.Length <= MaxLoggedTextChars
            ? normalized
            : $"{normalized[..MaxLoggedTextChars]}...(truncated, total={normalized.Length})";
    }

    private async Task<bool> SendAsyncInternal(ClientWebSocket ws, object payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ws.State != WebSocketState.Open) return false;
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task WaitForEventAsync(ClientWebSocket ws, string expectedEvent, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            var message = await ReceiveTextMessageAsync(ws, timeoutCts.Token).ConfigureAwait(false);
            if (message == null) throw new InvalidOperationException($"MiniMax websocket closed before '{expectedEvent}'.");

            if (TryGetEvent(message, out var eventName))
            {
                if (eventName == expectedEvent) return;
                if (eventName == "task_failed")
                    throw new InvalidOperationException($"MiniMax task failed while waiting for '{expectedEvent}'.");
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, int generation, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var message = await ReceiveTextMessageAsync(ws, cancellationToken).ConfigureAwait(false);
                if (message == null)
                {
                    // Server closed the socket. If this is still the active generation (i.e. not a
                    // local interrupt/stop, which bump the generation first), force-complete so a
                    // turn awaiting synthesis does not hang.
                    if (generation == _generation)
                        await ForceCompleteTurnAsync().ConfigureAwait(false);

                    return;
                }

                await HandleServerMessageAsync(message, generation).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi][MiniMaxTts] Receive loop failed.");
            if (generation == _generation)
            {
                await RaiseSynthesisFailedAsync(new RealtimeAiErrorData
                {
                    Code = "MiniMaxReceiveLoopFailed",
                    Message = ex.Message,
                    IsCritical = false
                }).ConfigureAwait(false);
                await ForceCompleteTurnAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task HandleServerMessageAsync(string message, int generation)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (TryGetEvent(root, out var eventName) && eventName == "task_failed")
            {
                if (generation != _generation) return;

                Log.Warning("[RealtimeAi][MiniMaxTts] task_failed received: {Message}", message);
                await RaiseSynthesisFailedAsync(new RealtimeAiErrorData
                {
                    Code = "MiniMaxTaskFailed",
                    Message = ExtractTaskFailedMessage(root, message),
                    IsCritical = false
                }).ConfigureAwait(false);
                await ForceCompleteTurnAsync().ConfigureAwait(false);
                return;
            }

            if (MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioPayload(root, out var audioBytes))
            {
                var sourceSampleRate = _assumedSourceSampleRate;

                if (MiniMaxRealtimeAiTtsPayloadParser.TryExtractWavPcm16(audioBytes, out var wavSampleRate, out var wavPcm))
                {
                    audioBytes = wavPcm;
                    sourceSampleRate = wavSampleRate;

                    var previousWav = Volatile.Read(ref _loggedWavSampleRate);
                    if (previousWav == 0 || previousWav != wavSampleRate)
                    {
                        Interlocked.Exchange(ref _loggedWavSampleRate, wavSampleRate);
                        Log.Information("[RealtimeAi][MiniMaxTts] WAV payload detected, SampleRate: {SampleRate}", wavSampleRate);
                    }
                }

                if (MiniMaxRealtimeAiTtsPayloadParser.TryGetAudioSampleRate(root, out var audioSampleRate) && audioSampleRate > 0)
                {
                    sourceSampleRate = audioSampleRate;
                    var previous = Volatile.Read(ref _loggedAudioSampleRate);
                    if (previous == 0 || previous != audioSampleRate)
                    {
                        Interlocked.Exchange(ref _loggedAudioSampleRate, audioSampleRate);
                        Log.Information("[RealtimeAi][MiniMaxTts] audio_sample_rate: {SampleRate}", audioSampleRate);
                    }
                }
                else
                {
                    var previousFallback = Volatile.Read(ref _loggedFallbackSampleRate);
                    if (previousFallback == 0 || previousFallback != sourceSampleRate)
                    {
                        Interlocked.Exchange(ref _loggedFallbackSampleRate, sourceSampleRate);
                        Log.Information("[RealtimeAi][MiniMaxTts] audio_sample_rate missing, fallback source_sample_rate: {SampleRate}", sourceSampleRate);
                    }
                }

                if (sourceSampleRate != _targetSampleRate)
                    audioBytes = AudioCodecConverter.Resample(audioBytes, sourceSampleRate, _targetSampleRate);

                if (generation == _generation)
                    await (AudioChunkReadyAsync?.Invoke(Convert.ToBase64String(audioBytes)) ?? Task.CompletedTask).ConfigureAwait(false);
            }

            var isFinal = root.TryGetProperty("is_final", out var finalProp) &&
                          finalProp.ValueKind == JsonValueKind.True;

            if (isFinal && generation == _generation)
                await OnSegmentSynthesizedAndCompleteIfReadyAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeAi][MiniMaxTts] Failed to parse server message.");
            if (generation != _generation) return;

            await RaiseSynthesisFailedAsync(new RealtimeAiErrorData
            {
                Code = "MiniMaxMessageParseFailed",
                Message = ex.Message,
                IsCritical = false
            }).ConfigureAwait(false);
            await ForceCompleteTurnAsync().ConfigureAwait(false);
        }
    }

    private void ClearTextState(bool clearPending)
    {
        lock (_textBufferLock)
        {
            _textBuffer.Clear();
        }

        lock (_turnStateLock)
        {
            _outstandingSegments = 0;
            _turnTextDone = false;
            _turnSynthesisRaised = false;
        }

        if (!clearPending) return;

        lock (_pendingSegmentsLock)
        {
            _pendingSegments.Clear();
        }
    }

    /// <summary>
    /// Resets per-turn synthesis tracking when a new turn's text starts arriving (the previous
    /// turn has already raised completion). Cheap no-op while a turn is in progress.
    /// </summary>
    private void BeginTurnIfCompleted()
    {
        lock (_turnStateLock)
        {
            if (!_turnSynthesisRaised) return;

            _turnSynthesisRaised = false;
            _turnTextDone = false;
            _outstandingSegments = 0;
        }
    }

    private async Task MarkTurnTextDoneAndCompleteIfReadyAsync()
    {
        bool raise;
        lock (_turnStateLock)
        {
            _turnTextDone = true;
            raise = TryClaimTurnCompletionLocked(force: false);
        }

        if (raise) await InvokeSynthesisCompletedAsync().ConfigureAwait(false);
    }

    private async Task OnSegmentSynthesizedAndCompleteIfReadyAsync()
    {
        bool raise;
        lock (_turnStateLock)
        {
            if (_outstandingSegments > 0) _outstandingSegments--;
            raise = TryClaimTurnCompletionLocked(force: false);
        }

        if (raise) await InvokeSynthesisCompletedAsync().ConfigureAwait(false);
    }

    private async Task ForceCompleteTurnAsync()
    {
        bool raise;
        lock (_turnStateLock)
        {
            raise = TryClaimTurnCompletionLocked(force: true);
        }

        if (raise) await InvokeSynthesisCompletedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true exactly once per turn when completion should be raised. A normal completion
    /// requires the provider text to be done and every accepted segment finalized; <paramref name="force"/>
    /// short-circuits that for terminal conditions (task_failed, parse error, socket closed).
    /// Caller must hold <see cref="_turnStateLock"/>.
    /// </summary>
    private bool TryClaimTurnCompletionLocked(bool force)
    {
        if (_turnSynthesisRaised) return false;
        if (!force && !(_turnTextDone && _outstandingSegments <= 0)) return false;

        _turnSynthesisRaised = true;
        return true;
    }

    private async Task InvokeSynthesisCompletedAsync()
    {
        await (SynthesisCompletedAsync?.Invoke() ?? Task.CompletedTask).ConfigureAwait(false);
    }

    private async Task RaiseSynthesisFailedAsync(RealtimeAiErrorData errorData)
    {
        await (SynthesisFailedAsync?.Invoke(errorData) ?? Task.CompletedTask).ConfigureAwait(false);
    }

    private static string ExtractTaskFailedMessage(JsonElement root, string fallback)
    {
        if (root.TryGetProperty("base_resp", out var baseResp) && baseResp.ValueKind == JsonValueKind.Object)
        {
            if (baseResp.TryGetProperty("status_msg", out var statusMsg) && statusMsg.ValueKind == JsonValueKind.String)
                return statusMsg.GetString();

            if (baseResp.TryGetProperty("message", out var baseMessage) && baseMessage.ValueKind == JsonValueKind.String)
                return baseMessage.GetString();
        }

        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            return message.GetString();

        return BuildLogTextPreview(fallback);
    }

    private static async Task<string> ReceiveTextMessageAsync(ClientWebSocket ws, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            if (result.Count > 0)
                stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }
    }

    private List<string> DrainCompletedSegmentsLocked(bool flushAll)
    {
        var segments = new List<string>();
        if (_textBuffer.Length == 0) return segments;

        if (flushAll)
        {
            var text = _textBuffer.ToString().Trim();
            _textBuffer.Clear();
            if (!string.IsNullOrWhiteSpace(text))
                segments.Add(text);

            return segments;
        }

        var consumed = 0;
        for (var i = 0; i < _textBuffer.Length; i++)
        {
            if (!SentenceBoundaries.Contains(_textBuffer[i])) continue;

            var candidate = _textBuffer.ToString(consumed, i - consumed + 1).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                consumed = i + 1;
                continue;
            }

            if (candidate.Length < MinStreamingSegmentChars)
                continue;

            consumed = i + 1;
            segments.Add(candidate);
        }

        if (consumed > 0)
            _textBuffer.Remove(0, consumed);

        return segments;
    }

    private static bool TryGetEvent(string json, out string eventName)
    {
        eventName = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryGetEvent(doc.RootElement, out eventName);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetEvent(JsonElement root, out string eventName)
    {
        eventName = string.Empty;

        if (!root.TryGetProperty("event", out var eventProp) || eventProp.ValueKind != JsonValueKind.String)
            return false;

        eventName = eventProp.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(eventName);
    }

    private static string GetStringConfig(IDictionary<string, object> config, string key, string defaultValue)
    {
        if (!config.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString() ?? defaultValue,
            _ => defaultValue
        };
    }

    private static int GetIntConfig(IDictionary<string, object> config, string key, int defaultValue)
    {
        if (!config.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            int number => number,
            long number => (int)number,
            double number => (int)number,
            decimal number => (int)number,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static double GetDoubleConfig(IDictionary<string, object> config, string key, double defaultValue)
    {
        if (!config.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        return value switch
        {
            double number => number,
            float number => number,
            decimal number => (double)number,
            int number => number,
            long number => number,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetDouble(out var parsed) => parsed,
            string text when double.TryParse(text, out var parsed) => parsed,
            _ => defaultValue
        };
    }
}
