using System.Net.WebSockets;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private bool IsProviderSessionActive => _ctx.SessionCts is { IsCancellationRequested: false };

    private async Task OnWssMessageReceivedAsync(string rawMessage)
    {
        if (!IsProviderSessionActive) return;

        var parsedEvent = _ctx.ProviderAdapter.ParseMessage(rawMessage);

        try
        {
            switch (parsedEvent.Type)
            {
                case RealtimeAiWssEventType.SessionInitialized:
                    Log.Information("[RealtimeAi] Provider session initialized, SessionId: {SessionId}", _ctx.SessionId);
                    await OnSessionInitializedAsync().ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.ResponseAudioDelta:
                    if (parsedEvent.Data is RealtimeAiWssAudioData audioData)
                        await OnAiAudioOutputReadyAsync(audioData).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.InputAudioTranscriptionPartial:
                case RealtimeAiWssEventType.InputAudioTranscriptionCompleted:
                case RealtimeAiWssEventType.OutputAudioTranscriptionPartial:
                case RealtimeAiWssEventType.OutputAudioTranscriptionCompleted:
                    if (parsedEvent.Data is RealtimeAiWssTranscriptionData transcription)
                        await OnTranscriptionReceivedAsync(parsedEvent.Type, transcription).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.SpeechDetected:
                    await OnAiDetectedUserSpeechAsync().ConfigureAwait(false);
                    break;

                // Both originate from provider's response.done —
                // FunctionCallSuggested when the response contains function calls, ResponseTurnCompleted otherwise.
                case RealtimeAiWssEventType.FunctionCallSuggested:
                case RealtimeAiWssEventType.ResponseTurnCompleted:
                    if (parsedEvent.Data is List<RealtimeAiWssFunctionCallData> functionCalls)
                        await OnFunctionCallsReceivedAsync(functionCalls).ConfigureAwait(false);
                    await OnAiTurnCompletedAsync().ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.Error:
                    await OnProviderErrorAsync(parsedEvent.Data as RealtimeAiErrorData ?? new RealtimeAiErrorData { Message = parsedEvent.RawJson ?? "Unknown error", IsCritical = true }).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.ResponseAudioDone:
                    break;

                case RealtimeAiWssEventType.Unknown:
                    Log.Warning("[RealtimeAi] Unknown provider event, SessionId: {SessionId}, Data: {Data}, Raw: {RawJson}", _ctx.SessionId, parsedEvent.Data, parsedEvent.RawJson);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Failed to process provider message, SessionId: {SessionId}, EventType: {EventType}", _ctx.SessionId, parsedEvent.Type);
        }
    }

    private async Task OnWssStateChangedAsync(WebSocketState newState, string reason)
    {
        Log.Information("[RealtimeAi] Provider connection state changed, SessionId: {SessionId}, NewState: {NewState}, Reason: {Reason}", _ctx.SessionId, newState, reason);

        if ((newState == WebSocketState.Closed || newState == WebSocketState.Aborted) && IsProviderSessionActive)
            await OnProviderErrorAsync(new RealtimeAiErrorData { Code = "ConnectionLost", Message = $"Provider connection lost: {reason}", IsCritical = true }).ConfigureAwait(false);
    }

    private async Task OnWssErrorOccurredAsync(Exception ex)
    {
        Log.Error(ex, "[RealtimeAi] Provider WebSocket error, SessionId: {SessionId}", _ctx.SessionId);

        await OnProviderErrorAsync(new RealtimeAiErrorData { Code = "ProviderClientError", Message = ex.Message, IsCritical = true }).ConfigureAwait(false);
    }

    private async Task OnSessionInitializedAsync()
    {
        if (_ctx.Options.OnSessionReadyAsync != null)
            await _ctx.Options.OnSessionReadyAsync(_ctx.SessionActions).ConfigureAwait(false);
    }

    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        _ctx.IsAiSpeaking = true;

        var audioBytes = Convert.FromBase64String(aiAudioData.Base64Payload);

        await WriteToAudioBufferAsync(audioBytes).ConfigureAwait(false);

        var providerCodec = _ctx.ProviderAdapter.GetPreferredCodec(_ctx.ClientAdapter.NativeAudioCodec);
        var clientAudioBytes = AudioCodecConverter.Convert(audioBytes, providerCodec, _ctx.ClientAdapter.NativeAudioCodec);
        var clientBase64 = Convert.ToBase64String(clientAudioBytes);

        await SendToClientAsync(_ctx.ClientAdapter.BuildAudioDeltaMessage(clientBase64, _ctx.SessionId)).ConfigureAwait(false);
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        _ctx.IsAiSpeaking = false;

        if (_ctx.Options.IdleFollowUp != null)
            _inactivityTimerManager.StopTimer(_ctx.SessionId);

        await SendToClientAsync(_ctx.ClientAdapter.BuildSpeechDetectedMessage(_ctx.SessionId)).ConfigureAwait(false);
    }

    private async Task OnAiTurnCompletedAsync()
    {
        _ctx.Round += 1;
        _ctx.IsAiSpeaking = false;

        var idleFollowUp = _ctx.Options.IdleFollowUp;

        if (idleFollowUp != null && (!idleFollowUp.SkipRounds.HasValue || idleFollowUp.SkipRounds.Value < _ctx.Round))
        {
            _inactivityTimerManager.StartTimer(_ctx.SessionId, TimeSpan.FromSeconds(idleFollowUp.TimeoutSeconds), async () =>
            {
                Log.Information("[RealtimeAi] Idle follow-up triggered, SessionId: {SessionId}, TimeoutSeconds: {TimeoutSeconds}", _ctx.SessionId, idleFollowUp.TimeoutSeconds);

                if (!string.IsNullOrEmpty(idleFollowUp.FollowUpMessage)) await SendTextToProviderAsync(idleFollowUp.FollowUpMessage);

                if (idleFollowUp.OnTimeoutAsync != null) await idleFollowUp.OnTimeoutAsync();
            });
        }

        await SendToClientAsync(_ctx.ClientAdapter.BuildTurnCompletedMessage(_ctx.SessionId)).ConfigureAwait(false);
        
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, Round: {Round}", _ctx.SessionId, _ctx.Round);
    }

    private async Task OnTranscriptionReceivedAsync(RealtimeAiWssEventType eventType, RealtimeAiWssTranscriptionData transcriptionData)
    {
        // Partial transcriptions are incremental fragments (e.g. "你" → "你好" → "你好，请问..."),
        // only sent to client for real-time UI display. Only completed transcriptions (full sentences)
        // are queued for final delivery via OnTranscriptionsCompletedAsync at session end.
        if (eventType != RealtimeAiWssEventType.OutputAudioTranscriptionPartial)
            _ctx.Transcriptions.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        await SendToClientAsync(_ctx.ClientAdapter.BuildTranscriptionMessage(eventType, transcriptionData, _ctx.SessionId)).ConfigureAwait(false);
    }
    
    private async Task OnFunctionCallsReceivedAsync(List<RealtimeAiWssFunctionCallData> functionCalls)
    {
        if (_ctx.Options.OnFunctionCallAsync == null) return;

        var replies = new List<(RealtimeAiWssFunctionCallData FunctionCall, string Output)>();

        foreach (var functionCall in functionCalls)
        {
            Log.Information("[RealtimeAi] Function call received, SessionId: {SessionId}, Function: {FunctionName}", _ctx.SessionId, functionCall.FunctionName);

            var result = await _ctx.Options.OnFunctionCallAsync(functionCall, _ctx.SessionActions).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(result?.Output)) replies.Add((functionCall, result.Output));
        }

        foreach (var (functionCall, output) in replies)
            await SendRawToProviderAsync(_ctx.ProviderAdapter.BuildFunctionCallReplyMessage(functionCall, output)).ConfigureAwait(false);

        // After sending all function_call_output items, explicitly trigger a new AI response
        // so the provider incorporates the results into its next reply.
        if (replies.Count > 0)
            await SendRawToProviderAsync(_ctx.ProviderAdapter.BuildTriggerResponseMessage()).ConfigureAwait(false);
    }

    private async Task OnProviderErrorAsync(RealtimeAiErrorData errorData)
    {
        Log.Error("[RealtimeAi] Provider error, SessionId: {SessionId}, Code: {ErrorCode}, Message: {ErrorMessage}, IsCritical: {IsCritical}", _ctx.SessionId, errorData.Code, errorData.Message, errorData.IsCritical);

        await SendToClientAsync(_ctx.ClientAdapter.BuildErrorMessage(errorData.Code, errorData.Message, _ctx.SessionId)).ConfigureAwait(false);

        if (errorData.IsCritical)
            await DisconnectFromProviderAsync($"Critical provider error: {errorData.Message}").ConfigureAwait(false);
    }
}
