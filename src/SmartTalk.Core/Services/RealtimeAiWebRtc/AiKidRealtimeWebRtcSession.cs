using System.Collections.Concurrent;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiKids;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Services.Timer;
using SmartTalk.Messages.Commands.AiKids;
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
}

public sealed class AiKidRealtimeWebRtcSession : IAiKidRealtimeWebRtcSession, IDisposable
{
    private readonly IAiKidRealtimeServiceV2 _aiKidRealtimeService;
    private readonly IRealtimeAiSwitcher _realtimeAiSwitcher;
    private readonly IOpenAiRealtimeWebRtcCallClient _callClient;
    private readonly IOpenAiRealtimeWebRtcSidebandClient _sidebandClient;
    private readonly IInactivityTimerManager _inactivityTimerManager;

    private readonly ConcurrentQueue<(AiSpeechAssistantSpeaker Speaker, string Text)> _transcriptions = new();
    private readonly SemaphoreSlim _responseStateLock = new(1, 1);
    private readonly CancellationTokenSource _stopCts = new();

    private RealtimeSessionOptions _options;
    private IRealtimeAiProviderAdapter _providerAdapter;
    private CancellationToken _sessionToken;
    private string _callId;
    private int _round;
    private int _clientReady;
    private int _cleanupStarted;
    private bool _isResponseInProgress;
    private bool _hasPendingResponseTrigger;

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

        // The WebRTC media flows directly between browser and OpenAI, so the server does not
        // receive PCM frames and must not invoke the legacy recording buffer/callback.
        _options.EnableRecording = false;

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

        await TryHangupCallAsync().ConfigureAwait(false);

        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await _sidebandClient.CloseAsync("SmartTalk WebRTC session ended", closeCts.Token).ConfigureAwait(false);

        if (_options?.OnTranscriptionsCompletedAsync != null)
        {
            await _options.OnTranscriptionsCompletedAsync(
                _callId,
                _transcriptions.ToList()).ConfigureAwait(false);
        }

        if (_options?.OnSessionEndedAsync != null)
            await _options.OnSessionEndedAsync(_callId).ConfigureAwait(false);

        Log.Information(
            "[RealtimeAiWebRtc] Session ended, CallId: {CallId}, Rounds: {Rounds}, Transcriptions: {TranscriptionCount}",
            _callId, _round, _transcriptions.Count);
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
        _stopCts.Dispose();
    }
}
