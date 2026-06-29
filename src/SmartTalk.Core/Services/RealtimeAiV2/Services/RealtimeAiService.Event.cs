using System.Net.WebSockets;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Tts;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiV2.Services;

public partial class RealtimeAiService
{
    private bool IsProviderSessionActive => _ctx.SessionCts is { IsCancellationRequested: false };

    // The engine routes text-mode behaviour off the negotiated OutputMode (decided once at connect),
    // not by re-inspecting the TTS provider's vendor type. Equivalent for any live session — the
    // negotiator returns Text iff the TTS needs text input (i.e. a non-BuiltIn provider) — but it keeps
    // OutputMode the single source of truth so a provider's type can't drift from the negotiated mode.
    private bool UsesExternalTts => _ctx.OutputMode == RealtimeAiOutputMode.Text;

    // The TTS provider implements exactly one direction sibling (audio passthrough vs text synthesizer);
    // routing through these casts means a provider structurally cannot receive the half it doesn't own.
    private IRealtimeAiAudioPassthrough AudioPassthrough => _ctx.TtsProvider as IRealtimeAiAudioPassthrough;

    private IRealtimeAiTextSynthesizer TextSynthesizer => _ctx.TtsProvider as IRealtimeAiTextSynthesizer;

    private async Task OnWssMessageReceivedAsync(string rawMessage)
    {
        if (!IsProviderSessionActive) return;

        var parsedEvent = _ctx.ProviderAdapter.ParseMessage(rawMessage);
        TryTrackLastAssistantItemId(parsedEvent);

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
                    {
                        if (!string.IsNullOrEmpty(audioData.ItemId))
                            _ctx.LastAssistantItemId = audioData.ItemId;

                        await (AudioPassthrough?.HandleProviderAudioAsync(audioData.Base64Payload, _ctx.SessionCts?.Token ?? CancellationToken.None) ?? Task.CompletedTask).ConfigureAwait(false);
                    }
                    break;

                case RealtimeAiWssEventType.ResponseTextDelta:
                    if (parsedEvent.Data is RealtimeAiWssTextData textDeltaData)
                        await ForwardProviderTextToTtsAsync(textDeltaData.Text).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.ResponseTextDone:
                    if (!_ctx.CurrentResponseTextDoneHandled)
                        await FlushProviderTextToTtsAsync((parsedEvent.Data as RealtimeAiWssTextData)?.Text).ConfigureAwait(false);
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
                    // Only the external-TTS (text) path consumes the provider's response text. In audio
                    // mode the BuiltIn provider's text handlers are no-ops and the transcript arrives via
                    // output_audio_transcript events, so gating here keeps the audio path from running the
                    // text-synthesis routing — production behaviour with BuiltIn is unchanged.
                    if (UsesExternalTts)
                    {
                        if (parsedEvent.Data is RealtimeAiWssTextData completedTextData)
                            await FlushProviderTextToTtsAsync(completedTextData.Text).ConfigureAwait(false);
                        else if (_ctx.CurrentResponseHasTextOutput && !_ctx.CurrentResponseTextDoneHandled)
                            await FlushProviderTextToTtsAsync().ConfigureAwait(false);
                    }

                    if (parsedEvent.Data is List<RealtimeAiWssFunctionCallData> functionCalls)
                        await OnFunctionCallsReceivedAsync(functionCalls).ConfigureAwait(false);
                    if (parsedEvent.Usage != null)
                        await OnResponseUsageReceivedAsync(parsedEvent.Usage).ConfigureAwait(false);
                    await MarkProviderTurnCompletedAndCompleteWhenReadyAsync().ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.Error:
                    await OnProviderErrorAsync(parsedEvent.Data as RealtimeAiErrorData ?? new RealtimeAiErrorData { Message = parsedEvent.RawJson ?? "Unknown error", IsCritical = true }).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.ResponseAudioDone:
                    await (AudioPassthrough?.HandleProviderAudioDoneAsync(_ctx.SessionCts?.Token ?? CancellationToken.None) ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                
                case RealtimeAiWssEventType.ResponseStarted:
                    ResetCurrentResponseState();
                    await MarkProviderResponseStartedAsync().ConfigureAwait(false);
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

        // Empty item_id must not clobber a previously-tracked id from earlier in the same turn.
        if (!string.IsNullOrEmpty(aiAudioData.ItemId))
            _ctx.LastAssistantItemId = aiAudioData.ItemId;

        // Anchor is set once per turn — first delta wins so subsequent deltas don't shift it.
        if (!_ctx.ResponseStartTimestampTwilio.HasValue && _ctx.LatestMediaTimestamp.HasValue)
            _ctx.ResponseStartTimestampTwilio = _ctx.LatestMediaTimestamp;

        var clientBase64 = await TranscodeAudioAsync(aiAudioData.Base64Payload, AudioSource.Provider).ConfigureAwait(false);

        await SendAudioToClientAsync(clientBase64).ConfigureAwait(false);
    }

    private void TryTrackLastAssistantItemId(ParsedRealtimeAiProviderEvent parsedEvent)
    {
        if (string.IsNullOrEmpty(parsedEvent.ItemId)) return;

        if (parsedEvent.Type is RealtimeAiWssEventType.ResponseStarted
            or RealtimeAiWssEventType.ResponseAudioDelta
            or RealtimeAiWssEventType.ResponseAudioDone
            or RealtimeAiWssEventType.ResponseTextDelta
            or RealtimeAiWssEventType.ResponseTextDone
            or RealtimeAiWssEventType.ResponseTurnCompleted
            or RealtimeAiWssEventType.FunctionCallSuggested)
        {
            _ctx.LastAssistantItemId = parsedEvent.ItemId;
        }
    }

    private Task OnTtsAudioChunkReadyAsync(string base64Payload)
    {
        return OnAiAudioOutputReadyAsync(new RealtimeAiWssAudioData { Base64Payload = base64Payload });
    }

    private async Task OnTtsSynthesisCompletedAsync()
    {
        _ctx.IsAiSpeaking = false;

        if (UsesExternalTts)
            await MarkTtsSynthesisCompletedAndCompleteWhenReadyAsync().ConfigureAwait(false);
    }

    private async Task OnTtsSynthesisFailedAsync(RealtimeAiErrorData errorData)
    {
        await OnProviderErrorAsync(errorData ?? new RealtimeAiErrorData
        {
            Code = "TtsSynthesisFailed",
            Message = "TTS synthesis failed.",
            IsCritical = false
        }).ConfigureAwait(false);

        if (UsesExternalTts)
            await MarkTtsSynthesisCompletedAndCompleteWhenReadyAsync().ConfigureAwait(false);
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        _ctx.IsAiSpeaking = false;

        if (_ctx.Options.IdleFollowUp != null)
            _inactivityTimerManager.StopTimer(_ctx.SessionId);

        // `clear` first (time-critical playback stop), truncate after (history correction).
        await SendToClientAsync(_ctx.ClientAdapter.BuildSpeechDetectedMessage(_ctx.SessionId)).ConfigureAwait(false);
        await SendBargeInTruncateIfApplicableAsync().ConfigureAwait(false);
        await _ctx.TtsProvider.HandleInterruptAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a provider truncate when item_id, stream clock, and per-turn anchor are
    /// all set. Skipped silently otherwise. Clears per-turn state after sending so a
    /// second speech-detected in the same turn cannot re-truncate the same item.
    /// </summary>
    private async Task SendBargeInTruncateIfApplicableAsync()
    {
        var itemId = _ctx.LastAssistantItemId;
        var clock = _ctx.LatestMediaTimestamp;
        var anchor = _ctx.ResponseStartTimestampTwilio;

        if (string.IsNullOrEmpty(itemId) || !clock.HasValue || !anchor.HasValue) return;

        // External TTS runs the provider in text-only mode, so the assistant item carries no audio
        // to truncate. Sending a truncate against a text item makes OpenAI return an error that the
        // engine classifies as critical (→ session disconnect). The TTS provider's HandleInterruptAsync
        // performs the actual playback stop, so we only correct provider history in built-in mode.
        if (!UsesExternalTts)
        {
            var elapsedMs = Math.Max(0L, clock.Value - anchor.Value);

            var truncateMessage = _ctx.ProviderAdapter.BuildTruncateMessage(itemId, elapsedMs);

            if (truncateMessage != null)
            {
                await SendToProviderAsync(truncateMessage).ConfigureAwait(false);
                Log.Information("[RealtimeAi] Barge-in truncate sent, SessionId: {SessionId}, ItemId: {ItemId}, AudioEndMs: {AudioEndMs}", _ctx.SessionId, itemId, elapsedMs);
            }
        }

        _ctx.LastAssistantItemId = null;
        _ctx.ResponseStartTimestampTwilio = null;
    }

    private async Task OnAiTurnCompletedAsync()
    {
        await MarkProviderResponseCompletedAndDrainAsync().ConfigureAwait(false);

        _ctx.Round += 1;
        _ctx.IsAiSpeaking = false;

        // Clear per-turn barge-in state. LatestMediaTimestamp keeps its value (running clock).
        _ctx.LastAssistantItemId = null;
        _ctx.ResponseStartTimestampTwilio = null;

        var idleFollowUp = _ctx.Options.IdleFollowUp;

        if (idleFollowUp != null && (!idleFollowUp.SkipRounds.HasValue || idleFollowUp.SkipRounds.Value < _ctx.Round))
        {
            _inactivityTimerManager.StartTimer(_ctx.SessionId, TimeSpan.FromSeconds(idleFollowUp.TimeoutSeconds), async () =>
            {
                Log.Information("[RealtimeAi] Idle follow-up triggered, SessionId: {SessionId}, TimeoutSeconds: {TimeoutSeconds}", _ctx.SessionId, idleFollowUp.TimeoutSeconds);

                if (!string.IsNullOrEmpty(idleFollowUp.FollowUpMessage))
                {
                    if (IsProviderSessionActive)
                        await SendTextToProviderAsync(idleFollowUp.FollowUpMessage);
                    else
                        Log.Warning("[RealtimeAi] Idle follow-up message skipped, session no longer active, SessionId: {SessionId}", _ctx.SessionId);
                }

                if (idleFollowUp.OnTimeoutAsync != null) await idleFollowUp.OnTimeoutAsync();
            });
        }

        await SendToClientAsync(_ctx.ClientAdapter.BuildTurnCompletedMessage(_ctx.SessionId)).ConfigureAwait(false);
        
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, Round: {Round}", _ctx.SessionId, _ctx.Round);
    }

    private async Task ForwardProviderTextToTtsAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // First text of this external turn → it will wait for the TTS gate; arm the absolute hard
        // ceiling so the turn can never hang past it (covers a provider that streams text then stalls
        // without ever sending response.done). Exactly-once is guaranteed by the same handled latch.
        // Gated on external-TTS mode: in audio mode the turn completes on provider-done without waiting,
        // so a hard-ceiling watchdog must never arm there (defensive — audio mode emits no text today).
        if (UsesExternalTts && !_ctx.CurrentResponseHasTextOutput) ArmTurnHardCeilingWatchdog();

        _ctx.CurrentResponseHasTextOutput = true;
        _ctx.CurrentResponseTtsSynthesisCompleted = false;
        _ctx.CurrentResponseTextBuilder.Append(text);

        await (TextSynthesizer?.HandleProviderTextDeltaAsync(text, _ctx.SessionCts?.Token ?? CancellationToken.None) ?? Task.CompletedTask).ConfigureAwait(false);
    }

    private async Task FlushProviderTextToTtsAsync(string completedText = null)
    {
        if (_ctx.CurrentResponseTextDoneHandled) return;

        if (!string.IsNullOrWhiteSpace(completedText) && !_ctx.CurrentResponseHasTextOutput)
            await ForwardProviderTextToTtsAsync(completedText).ConfigureAwait(false);

        _ctx.CurrentResponseTextDoneHandled = true;

        await EmitAssistantTextTranscriptIfApplicableAsync(completedText).ConfigureAwait(false);

        await (TextSynthesizer?.HandleProviderTextDoneAsync(_ctx.SessionCts?.Token ?? CancellationToken.None) ?? Task.CompletedTask).ConfigureAwait(false);
    }

    /// <summary>
    /// In external-TTS mode the provider emits text only, so no <c>output_audio_transcript</c>
    /// events arrive and the AI side of the transcript would otherwise be lost. Surface the
    /// assistant's turn text through the normal transcription path so both the saved transcript
    /// and the live client display still include it. No-op for the built-in audio path.
    /// </summary>
    private async Task EmitAssistantTextTranscriptIfApplicableAsync(string completedText)
    {
        if (!UsesExternalTts) return;

        var transcript = (!string.IsNullOrWhiteSpace(completedText)
            ? completedText
            : _ctx.CurrentResponseTextBuilder.ToString())?.Trim();

        if (string.IsNullOrWhiteSpace(transcript)) return;

        await OnTranscriptionReceivedAsync(
            RealtimeAiWssEventType.OutputAudioTranscriptionCompleted,
            new RealtimeAiWssTranscriptionData
            {
                Transcript = transcript,
                Speaker = AiSpeechAssistantSpeaker.Ai
            }).ConfigureAwait(false);
    }

    private void ResetCurrentResponseState()
    {
        // Bump the turn generation first so any TTS-synthesis watchdog still pending from the previous
        // turn no-ops when it fires (it captured the old generation).
        Interlocked.Increment(ref _ctx.CurrentTurnGeneration);

        _ctx.CurrentResponseHasTextOutput = false;
        _ctx.CurrentResponseTextDoneHandled = false;
        _ctx.CurrentResponseProviderTurnCompleted = false;
        _ctx.CurrentResponseTtsSynthesisCompleted = false;
        _ctx.CurrentResponseTurnCompletedHandled = false;
        _ctx.CurrentResponseTextBuilder.Clear();
    }

    private async Task MarkProviderTurnCompletedAndCompleteWhenReadyAsync()
    {
        if (await MarkProviderTurnCompletedAndShouldCompleteAsync().ConfigureAwait(false))
            await OnAiTurnCompletedAsync().ConfigureAwait(false);
        else
            ArmTtsSynthesisWatchdog();   // provider turn done but external TTS hasn't signalled yet
    }

    private async Task MarkTtsSynthesisCompletedAndCompleteWhenReadyAsync()
    {
        if (await MarkTtsSynthesisCompletedAndShouldCompleteAsync().ConfigureAwait(false))
            await OnAiTurnCompletedAsync().ConfigureAwait(false);
    }

    private async Task<bool> MarkProviderTurnCompletedAndShouldCompleteAsync()
    {
        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;

        await _ctx.TurnCompletionStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _ctx.CurrentResponseProviderTurnCompleted = true;
            return TryMarkCurrentResponseTurnCompletedLocked();
        }
        finally
        {
            _ctx.TurnCompletionStateLock.Release();
        }
    }

    private async Task<bool> MarkTtsSynthesisCompletedAndShouldCompleteAsync()
    {
        var token = _ctx.SessionCts?.Token ?? CancellationToken.None;

        await _ctx.TurnCompletionStateLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _ctx.CurrentResponseTtsSynthesisCompleted = true;
            return TryMarkCurrentResponseTurnCompletedLocked();
        }
        finally
        {
            _ctx.TurnCompletionStateLock.Release();
        }
    }

    private bool TryMarkCurrentResponseTurnCompletedLocked()
    {
        if (!_ctx.CurrentResponseProviderTurnCompleted) return false;

        var waitsForExternalTts = UsesExternalTts && _ctx.CurrentResponseHasTextOutput;
        if (!waitsForExternalTts) return true;

        if (_ctx.CurrentResponseTurnCompletedHandled) return false;
        if (!_ctx.CurrentResponseTtsSynthesisCompleted) return false;

        _ctx.CurrentResponseTurnCompletedHandled = true;
        return true;
    }

    private async Task OnTranscriptionReceivedAsync(RealtimeAiWssEventType eventType, RealtimeAiWssTranscriptionData transcriptionData)
    {
        // Only completed transcriptions (full sentences) are queued for final delivery
        // via OnTranscriptionsCompletedAsync at session end. Partial transcriptions are
        // incremental fragments (e.g. "你" → "你好" → "你好，请问..."), only sent to client for real-time UI display.
        if (eventType is RealtimeAiWssEventType.InputAudioTranscriptionCompleted or RealtimeAiWssEventType.OutputAudioTranscriptionCompleted)
            _ctx.Transcriptions.Enqueue((transcriptionData.Speaker, transcriptionData.Transcript));

        await SendToClientAsync(_ctx.ClientAdapter.BuildTranscriptionMessage(eventType, transcriptionData, _ctx.SessionId)).ConfigureAwait(false);
    }
    
    private async Task OnResponseUsageReceivedAsync(RealtimeAiWssUsageData usage)
    {
        // Always log the breakdown — gives ops a free cost-tracking signal in
        // structured Serilog properties even when the consumer doesn't wire a callback.
        Log.Information(
            "[RealtimeAi] Token usage reported, SessionId: {SessionId}, Round: {Round}, " +
            "Total: {Total}, Input: {Input}, Output: {Output}, Cached: {Cached}, " +
            "InputAudio: {InputAudio}, InputText: {InputText}, OutputAudio: {OutputAudio}, OutputText: {OutputText}",
            _ctx.SessionId, _ctx.Round,
            usage.TotalTokens, usage.InputTokens, usage.OutputTokens, usage.CachedTokens,
            usage.InputAudioTokens, usage.InputTextTokens, usage.OutputAudioTokens, usage.OutputTextTokens);

        if (_ctx.Options.OnResponseUsageReceivedAsync == null) return;

        await _ctx.Options.OnResponseUsageReceivedAsync(usage).ConfigureAwait(false);
    }

    private async Task OnFunctionCallsReceivedAsync(List<RealtimeAiWssFunctionCallData> functionCalls)
    {
        if (_ctx.Options.OnFunctionCallAsync == null) return;

        var shouldTriggerResponse = false;
        var replies = new List<(RealtimeAiWssFunctionCallData FunctionCall, string Output)>();

        foreach (var functionCall in functionCalls)
        {
            Log.Information("[RealtimeAi] Function call received, SessionId: {SessionId}, Function: {FunctionName}", _ctx.SessionId, functionCall.FunctionName);

            var result = await _ctx.Options.OnFunctionCallAsync(functionCall, _ctx.SessionActions).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(result?.Output)) replies.Add((functionCall, result.Output));
            if (result?.ShouldTriggerResponse == true) shouldTriggerResponse = true;
        }

        foreach (var (functionCall, output) in replies)
            await SendToProviderAsync(_ctx.ProviderAdapter.BuildFunctionCallReplyMessage(functionCall, output)).ConfigureAwait(false);

        if (replies.Count > 0 || shouldTriggerResponse)
            await QueueOrTriggerProviderResponseAsync("function call").ConfigureAwait(false);
    }

    private async Task OnProviderErrorAsync(RealtimeAiErrorData errorData)
    {
        if (errorData.IsCritical)
            Log.Error("[RealtimeAi] Provider error, SessionId: {SessionId}, Code: {ErrorCode}, Message: {ErrorMessage}, IsCritical: {IsCritical}", _ctx.SessionId, errorData.Code, errorData.Message, errorData.IsCritical);
        else
            Log.Warning("[RealtimeAi] Recoverable provider error, SessionId: {SessionId}, Code: {ErrorCode}, Message: {ErrorMessage}", _ctx.SessionId, errorData.Code, errorData.Message);

        if (IsActiveResponseInProgressError(errorData))
        {
            await QueueProviderResponseRetryAsync().ConfigureAwait(false);
            return;
        }

        await SendToClientAsync(_ctx.ClientAdapter.BuildErrorMessage(errorData.Code, errorData.Message, _ctx.SessionId)).ConfigureAwait(false);

        if (errorData.IsCritical)
            await DisconnectFromProviderAsync($"Critical provider error: {errorData.Message}").ConfigureAwait(false);
    }

    private static bool IsActiveResponseInProgressError(RealtimeAiErrorData errorData)
    {
        if (errorData == null) return false;

        if (string.Equals(errorData.Code, "conversation_already_has_active_response", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrEmpty(errorData.Message) &&
               errorData.Message.Contains("active response in progress", StringComparison.OrdinalIgnoreCase);
    }

    private async Task QueueProviderResponseRetryAsync()
    {
        if (!IsProviderSessionActive) return;

        await _ctx.ProviderResponseStateLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            _ctx.HasPendingProviderResponseTrigger = true;
            _ctx.IsProviderResponseInProgress = true;
        }
        finally
        {
            _ctx.ProviderResponseStateLock.Release();
        }

        Log.Information("[RealtimeAi] Queued response trigger retry after provider active-response conflict, SessionId: {SessionId}", _ctx.SessionId);
    }
}
