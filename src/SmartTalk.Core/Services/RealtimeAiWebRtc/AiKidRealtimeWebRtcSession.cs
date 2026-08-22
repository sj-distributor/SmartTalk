using System.Collections.Concurrent;
using NAudio.Wave;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiKids;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.RealtimeAiV2.Recording;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Commands.AiKids;
using SmartTalk.Messages.Commands.RealtimeAiWebRtc;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiWebRtc;

public interface IAiKidRealtimeWebRtcSession : IScopedDependency
{
    Task<RealtimeAiWebRtcCallResult> InitializeAsync(
        int assistantId,
        RealtimeAiServerRegion region,
        string offerSdp,
        CancellationToken initializationCancellationToken,
        CancellationToken sessionCancellationToken);

    Task RunAsync(CancellationToken cancellationToken);

    Task MarkClientReadyAsync();

    Task<AppendRealtimeAiWebRtcRecordingResponse> AppendRecordingAsync(
        long sequence,
        ReadOnlyMemory<byte> pcmBytes,
        bool isFinal);
}

public sealed class AiKidRealtimeWebRtcSession : IAiKidRealtimeWebRtcSession, IDisposable
{
    private const long MaxRecordingBytes = 90L * 1024 * 1024;
    private const long MaxRecordingBytesPerSecond = 24_000L * sizeof(short) * 4;
    private const long RecordingRateBurstBytes = 512L * 1024;
    private static readonly TimeSpan RecordingFinalizationGrace = TimeSpan.FromSeconds(3);

    private readonly IAiKidRealtimeServiceV2 _aiKidRealtimeService;
    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly IOpenAiRealtimeWebRtcCallClient _callClient;
    private readonly IOpenAiRealtimeWebRtcSidebandClient _sidebandClient;
    private readonly IInactivityTimerManager _inactivityTimerManager;

    private readonly ConcurrentQueue<(AiSpeechAssistantSpeaker Speaker, string Text)> _transcriptions = new();
    private readonly SemaphoreSlim _recordingLock = new(1, 1);
    private readonly SemaphoreSlim _responseStateLock = new(1, 1);
    private readonly CancellationTokenSource _stopCts = new();
    private readonly TaskCompletionSource _recordingFinalizedSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private RealtimeSessionOptions _options;
    private IRealtimeAiProviderAdapter _providerAdapter;
    private CancellationToken _sessionToken;
    private string _callId;
    private int _round;
    private int _clientReady;
    private int _cleanupStarted;
    private bool _isResponseInProgress;
    private bool _hasPendingResponseTrigger;
    private IRecordingBuffer _recordingBuffer;
    private DateTimeOffset _recordingRateCheckedAt;
    private double _recordingRateTokens;
    private long _recordingBytes;
    private long _nextRecordingSequence;
    private bool _recordingFinalized;
    private bool _recordingAccepting;

    public AiKidRealtimeWebRtcSession(
        IAiKidRealtimeServiceV2 aiKidRealtimeService,
        IRealtimeAiSwitcher realtimeAiSwitcher,
        IOpenAiRealtimeWebRtcCallClient callClient,
        IOpenAiRealtimeWebRtcSidebandClient sidebandClient,
        IInactivityTimerManager inactivityTimerManager)
    {
        _aiKidRealtimeService = aiKidRealtimeService;
        _realtimeAiSwitcher = realtimeAiSwitcher;
        _callClient = callClient;
        _sidebandClient = sidebandClient;
        _inactivityTimerManager = inactivityTimerManager;
    }

    public async Task<RealtimeAiWebRtcCallResult> InitializeAsync(
        int assistantId,
        RealtimeAiServerRegion region,
        string offerSdp,
        CancellationToken initializationCancellationToken,
        CancellationToken sessionCancellationToken)
    {
        _sessionToken = sessionCancellationToken;
        _options = await _aiKidRealtimeService.BuildSessionOptionsAsync(
            new AiKidRealtimeCommand
            {
                AssistantId = assistantId,
                Region = region,
                OrderRecordType = PhoneOrderRecordType.TestLink
            },
            initializationCancellationToken,
            CancellationToken.None).ConfigureAwait(false);

        _options.EnableRecording = true;

        // Interview WebRTC always uses OpenAI's native audio. Keep external TTS settings scoped
        // to the existing WSS/telephony paths, even if MiniMax is enabled for this assistant.
        _options.TtsConfig = null;

        _providerAdapter = _realtimeAiSwitcher.ProviderAdapter(_options.ModelConfig.Provider);
        var sessionJson = OpenAiRealtimeWebRtcSessionPayloadBuilder.Build(_options, _providerAdapter);
        var call = await _callClient.CreateCallAsync(
            offerSdp,
            sessionJson,
            _options.ModelConfig.ServiceUrl,
            initializationCancellationToken).ConfigureAwait(false);

        _callId = call.CallId;
        await _sidebandClient.ConnectAsync(
            call.SidebandUri,
            call.SidebandHeaders,
            initializationCancellationToken).ConfigureAwait(false);

        if (_options.EnableRecording)
        {
            _recordingBuffer = RealtimeAiRecordingSettings.Create();
            _recordingRateCheckedAt = DateTimeOffset.UtcNow;
            _recordingRateTokens = RecordingRateBurstBytes;
            _recordingAccepting = true;
        }

        Log.Information(
            "[RealtimeAiWebRtc] Session initialized, CallId: {CallId}, AssistantId: {AssistantId}, Region: {Region}, Provider: {Provider}, Model: {Model}",
            _callId, assistantId, region, _options.ModelConfig.Provider, _options.ModelConfig.ModelName);

        return call;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopCts.Token);

        try
        {
            await _sidebandClient.RunReceiveLoopAsync(ProcessProviderMessageAsync, runCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
            Log.Information("[RealtimeAiWebRtc] Session cancelled, CallId: {CallId}", _callId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAiWebRtc] Sideband session failed, CallId: {CallId}", _callId);
        }
        finally
        {
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    public async Task MarkClientReadyAsync()
    {
        if (Interlocked.Exchange(ref _clientReady, 1) != 0) return;

        Log.Information("[RealtimeAiWebRtc] Browser media/data channel ready, CallId: {CallId}", _callId);

        if (_options.OnSessionReadyAsync != null)
            await _options.OnSessionReadyAsync(BuildSessionActions()).ConfigureAwait(false);
    }

    public async Task<AppendRealtimeAiWebRtcRecordingResponse> AppendRecordingAsync(
        long sequence,
        ReadOnlyMemory<byte> pcmBytes,
        bool isFinal)
    {
        await _recordingLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (sequence < 0 || (!isFinal && pcmBytes.IsEmpty) || pcmBytes.Length % sizeof(short) != 0)
                return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.InvalidSequence);

            if (sequence < _nextRecordingSequence ||
                (_recordingFinalized && isFinal && sequence == _nextRecordingSequence))
                return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.Duplicate);

            if (_options?.EnableRecording != true || !_recordingAccepting || _recordingBuffer == null)
                return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.Finalized);

            if (sequence != _nextRecordingSequence)
                return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.InvalidSequence);

            if (!pcmBytes.IsEmpty)
            {
                if (_recordingBytes + pcmBytes.Length > MaxRecordingBytes)
                    return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.RecordingLimitExceeded);

                var now = DateTimeOffset.UtcNow;
                var elapsedSeconds = Math.Max(0, (now - _recordingRateCheckedAt).TotalSeconds);
                _recordingRateTokens = Math.Min(
                    RecordingRateBurstBytes,
                    _recordingRateTokens + elapsedSeconds * MaxRecordingBytesPerSecond);
                _recordingRateCheckedAt = now;
                if (pcmBytes.Length > _recordingRateTokens)
                    return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.RateLimitExceeded);

                await _recordingBuffer.WriteAsync(pcmBytes).ConfigureAwait(false);
                _recordingRateTokens -= pcmBytes.Length;
                _recordingBytes += pcmBytes.Length;
                _nextRecordingSequence++;
            }

            if (isFinal)
            {
                _recordingFinalized = true;
                _recordingAccepting = false;
                _recordingFinalizedSignal.TrySetResult();
            }

            return RecordingResult(RealtimeAiWebRtcRecordingAppendStatus.Accepted);
        }
        finally
        {
            _recordingLock.Release();
        }
    }

    private AppendRealtimeAiWebRtcRecordingResponse RecordingResult(
        RealtimeAiWebRtcRecordingAppendStatus status)
    {
        return new AppendRealtimeAiWebRtcRecordingResponse
        {
            Status = status,
            NextSequence = _nextRecordingSequence
        };
    }

    internal async Task ProcessProviderMessageAsync(string rawMessage)
    {
        var parsedEvent = _providerAdapter.ParseMessage(rawMessage);

        try
        {
            switch (parsedEvent.Type)
            {
                case RealtimeAiWssEventType.ResponseStarted:
                    await MarkResponseStartedAsync().ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.SpeechDetected:
                    _inactivityTimerManager.StopTimer(_callId);
                    Log.Information(
                        "[RealtimeAiWebRtc] User speech detected; provider-native WebRTC interruption is active, CallId: {CallId}",
                        _callId);
                    break;

                case RealtimeAiWssEventType.InputAudioTranscriptionCompleted:
                case RealtimeAiWssEventType.OutputAudioTranscriptionCompleted:
                    if (parsedEvent.Data is RealtimeAiWssTranscriptionData transcription &&
                        !string.IsNullOrWhiteSpace(transcription.Transcript))
                    {
                        _transcriptions.Enqueue((transcription.Speaker, transcription.Transcript));
                    }
                    break;

                case RealtimeAiWssEventType.FunctionCallSuggested:
                case RealtimeAiWssEventType.ResponseTurnCompleted:
                    if (parsedEvent.Data is List<RealtimeAiWssFunctionCallData> functionCalls)
                        await HandleFunctionCallsAsync(functionCalls).ConfigureAwait(false);

                    if (parsedEvent.Usage != null)
                        await HandleUsageAsync(parsedEvent.Usage).ConfigureAwait(false);

                    await CompleteResponseAndDrainAsync().ConfigureAwait(false);
                    StartIdleFollowUpIfApplicable();
                    break;

                case RealtimeAiWssEventType.Error:
                    var error = parsedEvent.Data as RealtimeAiErrorData
                        ?? new RealtimeAiErrorData { Message = "Unknown provider error", IsCritical = true };
                    await HandleProviderErrorAsync(error).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.Unknown:
                    Log.Debug("[RealtimeAiWebRtc] Ignored provider event, CallId: {CallId}, Type: {Type}", _callId, parsedEvent.Data);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Match the legacy engine: one bad callback/event is logged but does not tear down
            // the provider connection or prevent later speech/function events from being handled.
            Log.Error(
                ex,
                "[RealtimeAiWebRtc] Failed to process provider event, CallId: {CallId}, EventType: {EventType}",
                _callId, parsedEvent.Type);
        }
    }

    private RealtimeAiSessionActions BuildSessionActions()
    {
        return new RealtimeAiSessionActions
        {
            SendTextToProviderAsync = SendTextToProviderAsync,
            SendAudioToClientAsync = _ => Task.FromException(
                new NotSupportedException("Direct server-to-browser audio is not available in the WebRTC path.")),
            SuspendClientAudioToProvider = () => throw new NotSupportedException(
                "Server-side microphone suspension is not available in the direct WebRTC path."),
            ResumeClientAudioToProvider = () => throw new NotSupportedException(
                "Server-side microphone resumption is not available in the direct WebRTC path."),
            GetRecordedAudioSnapshotAsync = () => Task.FromResult(Array.Empty<byte>())
        };
    }

    private async Task HandleFunctionCallsAsync(List<RealtimeAiWssFunctionCallData> functionCalls)
    {
        if (_options.OnFunctionCallAsync == null)
        {
            Log.Warning(
                "[RealtimeAiWebRtc] Function calls received without a server handler, CallId: {CallId}, Count: {Count}",
                _callId, functionCalls.Count);
            return;
        }

        var actions = BuildSessionActions();
        var replies = new List<(RealtimeAiWssFunctionCallData FunctionCall, string Output)>();
        var shouldTriggerResponse = false;

        foreach (var functionCall in functionCalls)
        {
            Log.Information(
                "[RealtimeAiWebRtc] Function call received, CallId: {CallId}, Function: {Function}",
                _callId, functionCall.FunctionName);

            var result = await _options.OnFunctionCallAsync(functionCall, actions).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result?.Output)) replies.Add((functionCall, result.Output));
            if (result?.ShouldTriggerResponse == true) shouldTriggerResponse = true;
        }

        foreach (var (functionCall, output) in replies)
        {
            await SendToProviderAsync(
                _providerAdapter.BuildFunctionCallReplyMessage(functionCall, output)).ConfigureAwait(false);
        }

        if (replies.Count > 0 || shouldTriggerResponse)
            await QueueOrTriggerResponseAsync("function call").ConfigureAwait(false);
    }

    private async Task HandleUsageAsync(RealtimeAiWssUsageData usage)
    {
        Log.Information(
            "[RealtimeAiWebRtc] Token usage, CallId: {CallId}, Round: {Round}, Total: {Total}, Input: {Input}, Output: {Output}, InputAudio: {InputAudio}, OutputAudio: {OutputAudio}",
            _callId, _round, usage.TotalTokens, usage.InputTokens, usage.OutputTokens,
            usage.InputAudioTokens, usage.OutputAudioTokens);

        if (_options.OnResponseUsageReceivedAsync != null)
            await _options.OnResponseUsageReceivedAsync(usage).ConfigureAwait(false);
    }

    private async Task SendTextToProviderAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        await SendToProviderAsync(_providerAdapter.BuildTextUserMessage(text, _callId)).ConfigureAwait(false);
        await QueueOrTriggerResponseAsync("text input").ConfigureAwait(false);
    }

    private Task SendToProviderAsync(string message)
    {
        return message == null
            ? Task.CompletedTask
            : _sidebandClient.SendAsync(message, _sessionToken);
    }

    private async Task MarkResponseStartedAsync()
    {
        await _responseStateLock.WaitAsync(_sessionToken).ConfigureAwait(false);
        try
        {
            _isResponseInProgress = true;
        }
        finally
        {
            _responseStateLock.Release();
        }
    }

    private async Task QueueOrTriggerResponseAsync(string source)
    {
        var shouldSend = false;

        await _responseStateLock.WaitAsync(_sessionToken).ConfigureAwait(false);
        try
        {
            if (_isResponseInProgress)
            {
                _hasPendingResponseTrigger = true;
                Log.Information(
                    "[RealtimeAiWebRtc] Response trigger queued, CallId: {CallId}, Source: {Source}",
                    _callId, source);
                return;
            }

            _hasPendingResponseTrigger = false;
            _isResponseInProgress = true;
            shouldSend = true;
        }
        finally
        {
            _responseStateLock.Release();
        }

        if (shouldSend)
            await SendResponseTriggerAsync().ConfigureAwait(false);
    }

    private async Task CompleteResponseAndDrainAsync()
    {
        var shouldSend = false;

        await _responseStateLock.WaitAsync(_sessionToken).ConfigureAwait(false);
        try
        {
            _isResponseInProgress = false;
            if (_hasPendingResponseTrigger)
            {
                _hasPendingResponseTrigger = false;
                _isResponseInProgress = true;
                shouldSend = true;
            }
        }
        finally
        {
            _responseStateLock.Release();
        }

        _round += 1;

        if (shouldSend)
            await SendResponseTriggerAsync().ConfigureAwait(false);
    }

    private async Task HandleProviderErrorAsync(RealtimeAiErrorData error)
    {
        if (error.IsCritical)
        {
            Log.Error(
                "[RealtimeAiWebRtc] Critical provider error, CallId: {CallId}, Code: {Code}, Message: {Message}",
                _callId, error.Code, error.Message);
        }
        else
        {
            Log.Warning(
                "[RealtimeAiWebRtc] Recoverable provider error, CallId: {CallId}, Code: {Code}, Message: {Message}",
                _callId, error.Code, error.Message);
        }

        if (IsActiveResponseInProgressError(error))
        {
            await QueueResponseTriggerRetryAsync().ConfigureAwait(false);
            return;
        }

        if (error.IsCritical)
            _stopCts.Cancel();
    }

    private static bool IsActiveResponseInProgressError(RealtimeAiErrorData error)
    {
        if (string.Equals(
                error.Code,
                "conversation_already_has_active_response",
                StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(error.Message) &&
               error.Message.Contains("active response in progress", StringComparison.OrdinalIgnoreCase);
    }

    private async Task QueueResponseTriggerRetryAsync()
    {
        await _responseStateLock.WaitAsync(_sessionToken).ConfigureAwait(false);
        try
        {
            _hasPendingResponseTrigger = true;
            _isResponseInProgress = true;
        }
        finally
        {
            _responseStateLock.Release();
        }

        Log.Information(
            "[RealtimeAiWebRtc] Queued response trigger retry after provider active-response conflict, CallId: {CallId}",
            _callId);
    }

    private async Task SendResponseTriggerAsync()
    {
        try
        {
            await SendToProviderAsync(_providerAdapter.BuildTriggerResponseMessage()).ConfigureAwait(false);
        }
        catch
        {
            await _responseStateLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                _isResponseInProgress = false;
                _hasPendingResponseTrigger = true;
            }
            finally
            {
                _responseStateLock.Release();
            }

            throw;
        }
    }

    private void StartIdleFollowUpIfApplicable()
    {
        var idle = _options.IdleFollowUp;
        if (idle == null || (idle.SkipRounds.HasValue && idle.SkipRounds.Value >= _round)) return;

        _inactivityTimerManager.StartTimer(_callId, TimeSpan.FromSeconds(idle.TimeoutSeconds), async () =>
        {
            if (!string.IsNullOrWhiteSpace(idle.FollowUpMessage))
                await SendTextToProviderAsync(idle.FollowUpMessage).ConfigureAwait(false);

            if (idle.OnTimeoutAsync != null)
                await idle.OnTimeoutAsync().ConfigureAwait(false);
        });
    }

    private async Task CleanupAsync()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0) return;

        _inactivityTimerManager.StopTimer(_callId);

        await WaitForRecordingFinalizationAsync().ConfigureAwait(false);

        await TryHangupCallAsync().ConfigureAwait(false);

        await SafeExecuteAsync(async () =>
        {
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _sidebandClient.CloseAsync("SmartTalk WebRTC session ended", closeCts.Token).ConfigureAwait(false);
        }, "close sideband").ConfigureAwait(false);

        await SafeExecuteAsync(
            () => _options?.OnSessionEndedAsync?.Invoke(_callId) ?? Task.CompletedTask,
            "invoke OnSessionEndedAsync").ConfigureAwait(false);
        await SafeExecuteAsync(HandleRecordingAsync, "handle recording").ConfigureAwait(false);
        await SafeExecuteAsync(HandleTranscriptionsAsync, "handle transcriptions").ConfigureAwait(false);

        Log.Information(
            "[RealtimeAiWebRtc] Session ended, CallId: {CallId}, Rounds: {Rounds}, Transcriptions: {TranscriptionCount}",
            _callId, _round, _transcriptions.Count);
    }

    private async Task HandleRecordingAsync()
    {
        var buffer = Interlocked.Exchange(ref _recordingBuffer, null);
        if (buffer == null) return;

        try
        {
            if (_options?.EnableRecording != true || _options.OnRecordingCompleteAsync == null) return;
            if (!_recordingFinalized)
            {
                Log.Warning(
                    "[RealtimeAiWebRtc] Discarding incomplete recording, CallId: {CallId}, Bytes: {Bytes}, NextSequence: {NextSequence}",
                    _callId,
                    _recordingBytes,
                    _nextRecordingSequence);
                return;
            }

            var pcmBytes = await buffer.ExtractAsync().ConfigureAwait(false);
            if (pcmBytes.Length == 0) return;

            var waveFormat = new WaveFormat(24_000, 16, 1);
            using var wavStream = new MemoryStream();
            await using (var writer = new WaveFileWriter(wavStream, waveFormat))
            {
                writer.Write(pcmBytes, 0, pcmBytes.Length);
                await writer.FlushAsync().ConfigureAwait(false);
            }

            await _options.OnRecordingCompleteAsync(_callId, wavStream.ToArray()).ConfigureAwait(false);
        }
        finally
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task WaitForRecordingFinalizationAsync()
    {
        var shouldWait = false;
        await _recordingLock.WaitAsync().ConfigureAwait(false);
        try
        {
            shouldWait = _recordingAccepting && _recordingBytes > 0;
            if (!shouldWait)
                _recordingAccepting = false;
        }
        finally
        {
            _recordingLock.Release();
        }

        if (!shouldWait) return;

        await Task.WhenAny(
            _recordingFinalizedSignal.Task,
            Task.Delay(RecordingFinalizationGrace)).ConfigureAwait(false);

        var autoFinalized = false;
        await _recordingLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _recordingAccepting = false;
            if (!_recordingFinalized && _recordingBytes > 0)
            {
                _recordingFinalized = true;
                autoFinalized = true;
                _recordingFinalizedSignal.TrySetResult();
            }
        }
        finally
        {
            _recordingLock.Release();
        }

        if (autoFinalized)
        {
            Log.Warning(
                "[RealtimeAiWebRtc] Auto-finalized incomplete recording during cleanup, CallId: {CallId}, Bytes: {Bytes}, NextSequence: {NextSequence}",
                _callId,
                _recordingBytes,
                _nextRecordingSequence);
        }
    }

    private Task HandleTranscriptionsAsync()
    {
        return _options?.OnTranscriptionsCompletedAsync == null || _transcriptions.IsEmpty
            ? Task.CompletedTask
            : _options.OnTranscriptionsCompletedAsync(_callId, _transcriptions.ToList());
    }

    private async Task SafeExecuteAsync(Func<Task> action, string operationName)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAiWebRtc] Cleanup failed: {Operation}, CallId: {CallId}", operationName, _callId);
        }
    }

    private async Task TryHangupCallAsync()
    {
        if (string.IsNullOrWhiteSpace(_callId) || string.IsNullOrWhiteSpace(_options?.ModelConfig?.ServiceUrl))
            return;

        using var hangupCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await _callClient.HangupCallAsync(
                _callId,
                _options.ModelConfig.ServiceUrl,
                hangupCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (hangupCts.IsCancellationRequested)
        {
            Log.Warning("[RealtimeAiWebRtc] Timed out hanging up call, CallId: {CallId}", _callId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeAiWebRtc] Failed to hang up call, CallId: {CallId}", _callId);
        }
    }

    public void Dispose()
    {
        _recordingLock.Dispose();
        _stopCts.Dispose();
    }
}
