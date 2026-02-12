using System.Net.WebSockets;
using Serilog;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private bool IsProviderSessionActive => _ctx.SessionCts is { IsCancellationRequested: false };

    private async Task OnWssMessageReceivedAsync(string rawMessage)
    {
        if (!IsProviderSessionActive) return;

        var parsedEvent = _ctx.Adapter.ParseMessage(rawMessage);

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

                case RealtimeAiWssEventType.InputAudioTranscriptionCompleted:
                case RealtimeAiWssEventType.OutputAudioTranscriptionPartial:
                case RealtimeAiWssEventType.OutputAudioTranscriptionCompleted:
                    if (parsedEvent.Data is RealtimeAiWssTranscriptionData transcription)
                        await OnTranscriptionReceivedAsync(parsedEvent.Type, transcription).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.SpeechDetected:
                    await OnAiDetectedUserSpeechAsync().ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.ResponseTurnCompleted:
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
            await _ctx.Options.OnSessionReadyAsync(SendTextToProviderAsync).ConfigureAwait(false);
    }

    private async Task OnAiAudioOutputReadyAsync(RealtimeAiWssAudioData aiAudioData)
    {
        if (aiAudioData == null || string.IsNullOrEmpty(aiAudioData.Base64Payload)) return;

        _ctx.IsAiSpeaking = true;

        var audioBytes = Convert.FromBase64String(aiAudioData.Base64Payload);

        await WriteToAudioBufferAsync(audioBytes).ConfigureAwait(false);

        var audioDelta = new
        {
            type = "ResponseAudioDelta",
            Data = new
            {
                aiAudioData.Base64Payload
            },
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(audioDelta).ConfigureAwait(false);
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        if (_ctx.Options.IdleFollowUp != null)
            _inactivityTimerManager.StopTimer(_ctx.StreamSid);

        var speechDetected = new
        {
            type = "SpeechDetected",
            session_id = _ctx.StreamSid
        };

        await SendToClientAsync(speechDetected).ConfigureAwait(false);
    }

    private async Task OnAiTurnCompletedAsync()
    {
        _ctx.Round += 1;
        _ctx.IsAiSpeaking = false;

        var turnCompleted = new
        {
            type = "AiTurnCompleted",
            session_id = _ctx.StreamSid
        };

        var idleFollowUp = _ctx.Options.IdleFollowUp;
        
        if (idleFollowUp != null && (!idleFollowUp.SkipRounds.HasValue || idleFollowUp.SkipRounds.Value < _ctx.Round))
        {
            _inactivityTimerManager.StartTimer(_ctx.StreamSid, TimeSpan.FromSeconds(idleFollowUp.TimeoutSeconds), async () =>
            {
                Log.Information("[RealtimeAi] Idle follow-up triggered, SessionId: {SessionId}, TimeoutSeconds: {TimeoutSeconds}", _ctx.SessionId, idleFollowUp.TimeoutSeconds);
                
                await SendTextToProviderAsync(idleFollowUp.FollowUpMessage);
            });
        }

        await SendToClientAsync(turnCompleted).ConfigureAwait(false);
        
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, Round: {Round}", _ctx.SessionId, _ctx.Round);
    }

    private async Task OnTranscriptionReceivedAsync(RealtimeAiWssEventType eventType, RealtimeAiWssTranscriptionData transcriptionData)
    {
        // Partial transcriptions are incremental fragments (e.g. "你" → "你好" → "你好，请问..."),
        // only sent to client for real-time UI display. Only completed transcriptions (full sentences)
        // are queued for final delivery via OnTranscriptionsCompletedAsync at session end.
        if (eventType != RealtimeAiWssEventType.OutputAudioTranscriptionPartial) 
            _ctx.Transcriptions.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        await SendToClientAsync(new
        {
            type = eventType.ToString(),
            Data = new { transcriptionData },
            session_id = _ctx.StreamSid
        }).ConfigureAwait(false);
    }
    
    private async Task OnProviderErrorAsync(RealtimeAiErrorData errorData)
    {
        Log.Error("[RealtimeAi] Provider error, SessionId: {SessionId}, Code: {ErrorCode}, Message: {ErrorMessage}, IsCritical: {IsCritical}", _ctx.SessionId, errorData.Code, errorData.Message, errorData.IsCritical);

        await SendToClientAsync(new
        {
            type = "ClientError",
            session_id = _ctx.StreamSid,
            Data = new { errorData.Code, errorData.Message }
        }).ConfigureAwait(false);

        if (errorData.IsCritical)
            await DisconnectFromProviderAsync($"Critical provider error: {errorData.Message}").ConfigureAwait(false);
    }
}
