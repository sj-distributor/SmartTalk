using System.Net.WebSockets;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Settings.RealtimeHttp;
using SmartTalk.Messages.Dto.RealtimeHttp;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeHttp;

public sealed class RealtimeHttpSession : IAsyncDisposable
{
    public const string IdleCloseReason = "idle_no_user_text_15s";

    private readonly string _sessionId;
    private readonly int _assistantId;
    private readonly RealtimeAiServerRegion _region;
    private readonly IRealtimeHttpGatewayTransport _transport;
    private readonly RealtimeHttpGatewaySettings _settings;
    private readonly IRealtimeHttpTtsService _ttsService;
    private readonly Action<RealtimeHttpSession, string> _onClosed;

    private readonly CancellationTokenSource _sessionCts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly List<string> _activeAssistantTranscripts = [];
    private readonly List<RealtimeHttpSessionEventDto> _recentEvents = [];

    private int _completedTurns;
    private int _closeSignaled;
    private string _status = "connected";
    private string _lastError = string.Empty;
    private string _lastEventType = string.Empty;
    private string _providerSessionId = string.Empty;
    private string _closeReason = string.Empty;
    private long _eventSequence;
    private global::System.Threading.Timer _idleTimer;
    private Task _receiveLoopTask;
    private TaskCompletionSource<(int TurnNumber, string OutputText)> _pendingTurnAwaiter;
    private int _pendingAwaiterTargetTurn;

    public RealtimeHttpSession(
        string sessionId,
        int assistantId,
        RealtimeAiServerRegion region,
        IRealtimeHttpGatewayTransport transport,
        RealtimeHttpGatewaySettings settings,
        IRealtimeHttpTtsService ttsService,
        Action<RealtimeHttpSession, string> onClosed)
    {
        _sessionId = sessionId;
        _assistantId = assistantId;
        _region = region;
        _transport = transport;
        _settings = settings;
        _ttsService = ttsService;
        _onClosed = onClosed;
    }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;

    public string SessionId => _sessionId;

    public string ProviderSessionId
    {
        get
        {
            lock (_stateLock)
            {
                return _providerSessionId;
            }
        }
    }

    public string CloseReason
    {
        get
        {
            lock (_stateLock)
            {
                return _closeReason;
            }
        }
    }

    public void StartReceiving()
    {
        _receiveLoopTask = Task.Run(ReceiveLoopSafelyAsync);
        RestartIdleTimer();
    }

    public RealtimeHttpCreateSessionResponse GetSnapshot()
    {
        lock (_stateLock)
        {
            return new RealtimeHttpCreateSessionResponse
            {
                SessionId = _sessionId,
                ProviderSessionId = _providerSessionId,
                AssistantId = _assistantId,
                Region = _region,
                Status = _status,
                CreatedAt = CreatedAt
            };
        }
    }

    public RealtimeHttpSessionDetailResponse GetDetail()
    {
        lock (_stateLock)
        {
            return new RealtimeHttpSessionDetailResponse
            {
                SessionId = _sessionId,
                ProviderSessionId = _providerSessionId,
                AssistantId = _assistantId,
                Region = _region,
                Status = _status,
                CreatedAt = CreatedAt,
                LastActivityAt = LastActivityAt,
                LastError = _lastError,
                CloseReason = _closeReason,
                CompletedTurns = _completedTurns,
                RecentEvents = _recentEvents.ToList()
            };
        }
    }

    public async Task<RealtimeHttpSendMessageResponse> SendTextAsync(
        string inputText,
        int? timeoutMs,
        CancellationToken cancellationToken)
    {
        if (!_sendLock.Wait(0))
            throw RealtimeHttpGatewayException.SessionBusy(_sessionId);

        try
        {
            CancelIdleTimer();
            EnsureOpen();

            lock (_stateLock)
            {
                _status = "processing_turn";
            }

            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sessionCts.Token);
            var sendToken = sendCts.Token;
            var waitTask = PrepareTurnAwaiter();
            AppendEvent("user_text_input", "user", inputText);

            var effectiveTimeoutMs = Math.Max(1000, timeoutMs ?? _settings.DefaultResponseTimeoutMs);
            var inputAudioDurationMs = 0;
            var tailSilenceMs = 0;

            if (_settings.SendTextAsTextInput)
            {
                await SendTextInputAsync(inputText, sendToken).ConfigureAwait(false);
            }
            else
            {
                var audioBytes = await _ttsService.SynthesizePcm16Async(inputText, sendToken).ConfigureAwait(false);
                if (audioBytes.Length == 0)
                    throw RealtimeHttpGatewayException.TtsUnavailable(_sessionId);

                inputAudioDurationMs = CalculatePcm16DurationMs(audioBytes.Length);
                tailSilenceMs = Math.Max(0, _settings.UserSpeechTailSilenceMs);
                var outboundAudio = AppendSilence(audioBytes, tailSilenceMs);
                await SendAudioChunksAsync(outboundAudio, sendToken).ConfigureAwait(false);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(sendToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(effectiveTimeoutMs));

            try
            {
                var (turnNumber, outputText) = await waitTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                return new RealtimeHttpSendMessageResponse
                {
                    SessionId = _sessionId,
                    ProviderSessionId = ProviderSessionId,
                    InputText = inputText,
                    OutputText = outputText,
                    Completed = true,
                    TurnNumber = turnNumber,
                    InputAudioDurationMs = inputAudioDurationMs,
                    TailSilenceMs = tailSilenceMs,
                    WaitTimeoutMs = effectiveTimeoutMs,
                    CompletionReason = "ai_turn_completed",
                    LastEventType = LastEventType,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_sessionCts.IsCancellationRequested)
            {
                throw RealtimeHttpGatewayException.AiResponseTimeout(_sessionId, ProviderSessionId, LastEventType);
            }
            catch (OperationCanceledException) when (_sessionCts.IsCancellationRequested)
            {
                throw CreateSessionClosedException();
            }
            catch (ObjectDisposedException) when (IsClosedOrClosing())
            {
                throw CreateSessionClosedException();
            }
            catch (InvalidOperationException) when (IsClosedOrClosing())
            {
                throw CreateSessionClosedException();
            }
        }
        catch (OperationCanceledException) when (_sessionCts.IsCancellationRequested)
        {
            throw CreateSessionClosedException();
        }
        catch (ObjectDisposedException) when (IsClosedOrClosing())
        {
            throw CreateSessionClosedException();
        }
        catch (InvalidOperationException) when (IsClosedOrClosing())
        {
            throw CreateSessionClosedException();
        }
        finally
        {
            ClearTurnAwaiter();
            if (!_sessionCts.IsCancellationRequested)
            {
                lock (_stateLock)
                {
                    if (_status != "closed" && _status != "closing")
                        _status = "idle_waiting";
                }

                RestartIdleTimer();
            }

            _sendLock.Release();
        }
    }

    public async Task<GatewayTurnWaitResult> WaitForNextTurnAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        if (!_sendLock.Wait(0))
            throw RealtimeHttpGatewayException.SessionBusy(_sessionId);

        try
        {
            CancelIdleTimer();
            EnsureOpen();
            var waitTask = PrepareTurnAwaiter();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sessionCts.Token);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMs)));

            try
            {
                var (turnNumber, outputText) = await waitTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                return new GatewayTurnWaitResult
                {
                    Completed = true,
                    TurnNumber = turnNumber,
                    OutputText = outputText
                };
            }
            catch (OperationCanceledException)
            {
                return new GatewayTurnWaitResult
                {
                    Completed = false,
                    TurnNumber = 0,
                    OutputText = string.Empty
                };
            }
        }
        finally
        {
            ClearTurnAwaiter();
            if (!_sessionCts.IsCancellationRequested)
                RestartIdleTimer();
            _sendLock.Release();
        }
    }

    public async Task CloseAsync(string reason, CancellationToken cancellationToken)
    {
        var shouldClose = false;
        lock (_stateLock)
        {
            if (_status != "closed" && _status != "closing")
            {
                _status = "closing";
                _closeReason = reason;
                shouldClose = true;
            }
        }

        if (!shouldClose)
        {
            SignalClosed(reason);
            return;
        }

        CancelIdleTimer();
        _sessionCts.Cancel();
        CompleteAwaiterIfNeeded();

        try
        {
            await _transport.CloseAsync(reason, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeHttpGateway] Transport close failed, SessionId: {SessionId}", _sessionId);
        }

        lock (_stateLock)
        {
            _status = "closed";
            _closeReason = reason;
        }

        SignalClosed(reason);
        await DisposeTransportAndTimerAsync().ConfigureAwait(false);
    }

    private async Task ReceiveLoopSafelyAsync()
    {
        try
        {
            while (!_sessionCts.IsCancellationRequested && _transport.State == WebSocketState.Open)
            {
                var rawMessage = await _transport.ReceiveTextAsync(_sessionCts.Token).ConfigureAwait(false);
                if (rawMessage == null)
                    break;

                LastActivityAt = DateTimeOffset.UtcNow;
                await HandleInboundMessageAsync(rawMessage).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal session close path.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeHttpGateway] Receive loop error, SessionId: {SessionId}", _sessionId);
            lock (_stateLock)
            {
                _status = "faulted";
                _lastError = ex.Message;
            }

            AppendEvent("gateway_error", "system", ex.Message);
        }
        finally
        {
            if (!_sessionCts.IsCancellationRequested)
                await CloseAsync("transport_closed", CancellationToken.None).ConfigureAwait(false);
            CompleteAwaiterIfNeeded();
        }
    }

    private async Task SendTextInputAsync(string text, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Serialize(new
        {
            text
        });

        await _transport.SendTextAsync(message, cancellationToken).ConfigureAwait(false);
        LastActivityAt = DateTimeOffset.UtcNow;
    }

    private async Task SendAudioChunksAsync(byte[] pcmBytes, CancellationToken cancellationToken)
    {
        if (pcmBytes.Length == 0) return;

        var bytesPerChunk = Math.Max(
            480,
            _settings.Tts.SampleRate * Math.Max(10, _settings.Tts.ChunkDurationMs) / 1000 * 2);
        var chunkIntervalMs = Math.Max(5, _settings.Tts.ChunkDurationMs);

        for (var offset = 0; offset < pcmBytes.Length; offset += bytesPerChunk)
        {
            var count = Math.Min(bytesPerChunk, pcmBytes.Length - offset);
            var slice = new byte[count];
            Buffer.BlockCopy(pcmBytes, offset, slice, 0, count);

            var message = JsonSerializer.Serialize(new
            {
                media = new
                {
                    type = "audio",
                    payload = Convert.ToBase64String(slice)
                }
            });

            await _transport.SendTextAsync(message, cancellationToken).ConfigureAwait(false);
            LastActivityAt = DateTimeOffset.UtcNow;

            if (_settings.RealTimeAudioPacingEnabled)
                await Task.Delay(chunkIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<(int TurnNumber, string OutputText)> PrepareTurnAwaiter()
    {
        lock (_stateLock)
        {
            _pendingAwaiterTargetTurn = _completedTurns + 1;
            _pendingTurnAwaiter = new TaskCompletionSource<(int TurnNumber, string OutputText)>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pendingTurnAwaiter.Task;
        }
    }

    private void ClearTurnAwaiter()
    {
        lock (_stateLock)
        {
            _pendingTurnAwaiter = null;
            _pendingAwaiterTargetTurn = 0;
        }
    }

    private Task HandleInboundMessageAsync(string rawMessage)
    {
        if (!TryGetRoot(rawMessage, out var root)) return Task.CompletedTask;

        if (TryGetStringProperty(root, "session_id", out var providerSessionId) && !string.IsNullOrWhiteSpace(providerSessionId))
        {
            lock (_stateLock)
            {
                _providerSessionId = providerSessionId;
            }
        }

        if (!TryGetStringProperty(root, "type", out var type)) return Task.CompletedTask;
        SetLastEventType(type);

        switch (type)
        {
            case "OutputAudioTranscriptionCompleted":
                var outputTranscript = ExtractTranscript(root);
                if (!string.IsNullOrWhiteSpace(outputTranscript))
                {
                    lock (_stateLock)
                    {
                        _activeAssistantTranscripts.Add(outputTranscript);
                    }
                    AppendEvent("assistant_transcript_completed", "assistant", outputTranscript);
                }
                break;

            case "InputAudioTranscriptionCompleted":
                var inputTranscript = ExtractTranscript(root);
                if (!string.IsNullOrWhiteSpace(inputTranscript))
                    AppendEvent("user_transcript_completed", "user", inputTranscript);
                break;

            case "AiTurnCompleted":
                _ = CompleteTurnAsync();
                break;

            case "ClientError":
                var err = ExtractClientError(root);
                TaskCompletionSource<(int TurnNumber, string OutputText)> pending;
                lock (_stateLock)
                {
                    _lastError = err;
                    pending = _pendingTurnAwaiter;
                }
                AppendEvent("provider_error", "system", err);
                pending?.TrySetException(RealtimeHttpGatewayException.ProviderError(_sessionId, ProviderSessionId, err));
                break;
        }

        return Task.CompletedTask;
    }

    private async Task CompleteTurnAsync()
    {
        if (_settings.TurnCompletionTranscriptGraceMs > 0)
        {
            try
            {
                await Task.Delay(_settings.TurnCompletionTranscriptGraceMs, _sessionCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                CompleteAwaiterIfNeeded();
                return;
            }
        }

        string outputText;
        int turnNumber;
        TaskCompletionSource<(int TurnNumber, string OutputText)> pending = null;

        lock (_stateLock)
        {
            turnNumber = ++_completedTurns;
            outputText = string.Join(" ", _activeAssistantTranscripts.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            _activeAssistantTranscripts.Clear();

            if (_pendingTurnAwaiter != null && turnNumber >= _pendingAwaiterTargetTurn)
                pending = _pendingTurnAwaiter;
        }

        AppendEvent("assistant_turn_completed", "assistant", outputText);
        pending?.TrySetResult((turnNumber, outputText));
    }

    private void CompleteAwaiterIfNeeded()
    {
        TaskCompletionSource<(int TurnNumber, string OutputText)> pending;
        lock (_stateLock)
        {
            pending = _pendingTurnAwaiter;
        }

        pending?.TrySetCanceled();
    }

    private void AppendEvent(string type, string role, string content)
    {
        var evt = new RealtimeHttpSessionEventDto
        {
            Sequence = Interlocked.Increment(ref _eventSequence),
            Type = type,
            Role = role,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow
        };

        lock (_stateLock)
        {
            _recentEvents.Add(evt);
            while (_recentEvents.Count > Math.Max(20, _settings.RecentEventCapacity))
                _recentEvents.RemoveAt(0);
        }
    }

    private void RestartIdleTimer()
    {
        var idleTimeoutMs = Math.Max(1, _settings.IdleTimeoutMs);
        lock (_stateLock)
        {
            if (_status is "closed" or "closing") return;
            _status = "idle_waiting";
        }

        _idleTimer ??= new global::System.Threading.Timer(_ => _ = CloseAsync(IdleCloseReason, CancellationToken.None));
        _idleTimer.Change(idleTimeoutMs, Timeout.Infinite);
    }

    private void CancelIdleTimer()
    {
        _idleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void EnsureOpen()
    {
        lock (_stateLock)
        {
            if (_status is "closed" or "closing")
                throw CreateSessionClosedException();
        }

        if (_transport.State != WebSocketState.Open)
            throw RealtimeHttpGatewayException.SessionClosed(_sessionId, ProviderSessionId, "transport_closed");
    }

    private bool IsClosedOrClosing()
    {
        lock (_stateLock)
        {
            return _status is "closed" or "closing" || _sessionCts.IsCancellationRequested;
        }
    }

    private RealtimeHttpGatewayException CreateSessionClosedException()
    {
        lock (_stateLock)
        {
            return RealtimeHttpGatewayException.SessionClosed(_sessionId, _providerSessionId, _closeReason);
        }
    }

    private void SignalClosed(string reason)
    {
        if (Interlocked.Exchange(ref _closeSignaled, 1) == 0)
            _onClosed(this, reason);
    }

    private int CalculatePcm16DurationMs(int byteLength)
    {
        var sampleRate = Math.Max(1, _settings.Tts.SampleRate);
        return (int)Math.Ceiling(byteLength / 2d / sampleRate * 1000d);
    }

    private byte[] AppendSilence(byte[] pcmBytes, int silenceMs)
    {
        if (silenceMs <= 0) return pcmBytes;

        var silenceBytes = Math.Max(0, _settings.Tts.SampleRate) * silenceMs / 1000 * 2;
        if (silenceBytes == 0) return pcmBytes;

        var result = new byte[pcmBytes.Length + silenceBytes];
        Buffer.BlockCopy(pcmBytes, 0, result, 0, pcmBytes.Length);
        return result;
    }

    private string LastEventType
    {
        get
        {
            lock (_stateLock)
            {
                return _lastEventType;
            }
        }
    }

    private void SetLastEventType(string eventType)
    {
        lock (_stateLock)
        {
            _lastEventType = eventType;
        }
    }

    private static bool TryGetRoot(string rawMessage, out JsonElement root)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawMessage);
            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            root = default;
            return false;
        }
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
    {
        if (TryGetPropertyIgnoreCase(element, propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string ExtractTranscript(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "Data", out var data)) return string.Empty;
        if (!TryGetPropertyIgnoreCase(data, "transcriptionData", out var transcriptionData)) return string.Empty;
        return TryGetStringProperty(transcriptionData, "Transcript", out var transcript) ? transcript : string.Empty;
    }

    private static string ExtractClientError(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "Data", out var data)) return "unknown_error";
        return TryGetStringProperty(data, "Message", out var message) ? message : "unknown_error";
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            CancelIdleTimer();
            _sessionCts.Cancel();
        }
        catch
        {
            // ignored
        }

        await DisposeTransportAndTimerAsync().ConfigureAwait(false);

        _sessionCts.Dispose();
    }

    private async Task DisposeTransportAndTimerAsync()
    {
        try
        {
            _idleTimer?.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }

    public class GatewayTurnWaitResult
    {
        public bool Completed { get; init; }

        public int TurnNumber { get; init; }

        public string OutputText { get; init; } = string.Empty;
    }
}
