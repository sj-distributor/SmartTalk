using System.Diagnostics;
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

        Interlocked.Exchange(ref _ctx.LastProviderMessageAt, Stopwatch.GetTimestamp());

        var parsedEvent = _ctx.ProviderAdapter.ParseMessage(rawMessage);
        TryTrackLastAssistantItemId(parsedEvent);
        ClearAwaitingProviderResponse(parsedEvent.Type);

        try
        {
            switch (parsedEvent.Type)
            {
                case RealtimeAiWssEventType.SessionInitialized:
                    // "Connected" only means the socket opened; this is the provider actually
                    // accepting the session config, which is the point the call can start.
                    Log.Information("[RealtimeAi] Provider session initialized, SessionId: {SessionId}, ElapsedProviderReadyMs: {ElapsedProviderReadyMs}",
                        _ctx.SessionId, (long)Stopwatch.GetElapsedTime(_ctx.ProviderConnectedAt).TotalMilliseconds);
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

                    try
                    {
                        if (parsedEvent.Data is List<RealtimeAiWssFunctionCallData> functionCalls)
                            await OnFunctionCallsReceivedAsync(functionCalls).ConfigureAwait(false);
                        if (parsedEvent.Usage != null)
                            await OnResponseUsageReceivedAsync(parsedEvent.Usage).ConfigureAwait(false);
                    }
                    finally
                    {
                        // The turn must complete even when handling its payload failed. Skipping this
                        // leaves the turn open forever: no idle timer, nothing to move the call on,
                        // and the caller waiting on an AI that will never speak again.
                        await MarkProviderTurnCompletedAndCompleteWhenReadyAsync().ConfigureAwait(false);
                    }
                    break;

                case RealtimeAiWssEventType.Error:
                    await OnProviderErrorAsync(parsedEvent.Data as RealtimeAiErrorData ?? new RealtimeAiErrorData { Message = parsedEvent.RawJson ?? "Unknown error", IsCritical = true }).ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.ResponseAudioDone:
                    await (AudioPassthrough?.HandleProviderAudioDoneAsync(_ctx.SessionCts?.Token ?? CancellationToken.None) ?? Task.CompletedTask).ConfigureAwait(false);
                    break;
                
                case RealtimeAiWssEventType.ResponseStarted:
                    await ResetCurrentResponseStateAsync().ConfigureAwait(false);
                    // Without this the only per-turn line is the completion, so a turn that never
                    // finishes leaves no trace that it began.
                    Log.Information("[RealtimeAi] Turn started, SessionId: {SessionId}, Round: {Round}, TurnGeneration: {TurnGeneration}, OutputMode: {OutputMode}",
                        _ctx.SessionId, _ctx.Round, Interlocked.Read(ref _ctx.CurrentTurnGeneration), _ctx.OutputMode);
                    await MarkProviderResponseStartedAsync().ConfigureAwait(false);
                    break;

                case RealtimeAiWssEventType.Ignored:
                    Log.Debug("[RealtimeAi] Ignored provider event, SessionId: {SessionId}, ProviderEventType: {ProviderEventType}", _ctx.SessionId, parsedEvent.Data);
                    break;

                case RealtimeAiWssEventType.Unknown:
                    // The raw frame is deliberately absent: it carries function-call arguments, i.e.
                    // the customer's order, name and phone. The event type is the diagnostic part.
                    Log.Warning("[RealtimeAi] Unknown provider event, SessionId: {SessionId}, ProviderEventType: {ProviderEventType}", _ctx.SessionId, parsedEvent.Data);
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

        ReportFirstAudioOfTurnIfNeeded();

        var clientBase64 = await TranscodeAudioAsync(aiAudioData.Base64Payload, AudioSource.Provider).ConfigureAwait(false);

        await SendAudioToClientAsync(clientBase64).ConfigureAwait(false);
    }

    /// <summary>
    /// Time from the provider starting a response to the first audio actually leaving for the
    /// caller — the number that maps onto what a caller experiences as a pause. Once per turn: the
    /// deltas that follow are the same turn still speaking.
    /// </summary>
    private void ReportFirstAudioOfTurnIfNeeded()
    {
        if (_ctx.TurnFirstAudioReported || _ctx.TurnStartedAt == 0) return;

        _ctx.TurnFirstAudioReported = true;

        Log.Information("[RealtimeAi] First audio of turn, SessionId: {SessionId}, Round: {Round}, ElapsedToFirstAudioMs: {ElapsedToFirstAudioMs}",
            _ctx.SessionId, _ctx.Round, (long)Stopwatch.GetElapsedTime(_ctx.TurnStartedAt).TotalMilliseconds);
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

    /// <summary>
    /// Closes the listening window on the first thing the provider produces of its own.
    ///
    /// <para>Read off the parsed event type rather than from a provider-specific start signal: the
    /// Google adapter never emits ResponseStarted at all, so keying on that would leave a Google
    /// session permanently awaiting after its first barge-in and report every ordinary caller pause as
    /// provider silence. Transcriptions of the CALLER are deliberately absent from this list — they are
    /// the provider echoing the caller back, not the provider answering.</para>
    /// </summary>
    private void ClearAwaitingProviderResponse(RealtimeAiWssEventType eventType)
    {
        if (eventType is RealtimeAiWssEventType.ResponseStarted
            or RealtimeAiWssEventType.ResponseAudioDelta
            or RealtimeAiWssEventType.ResponseAudioDone
            or RealtimeAiWssEventType.ResponseTextDelta
            or RealtimeAiWssEventType.ResponseTextDone
            or RealtimeAiWssEventType.ResponseTurnCompleted
            or RealtimeAiWssEventType.FunctionCallSuggested)
            _ctx.IsAwaitingProviderResponse = false;
    }

    private async Task OnAiDetectedUserSpeechAsync()
    {
        _ctx.IsAiSpeaking = false;
        _ctx.IsAwaitingProviderResponse = true;

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

        // The index of the turn being described, captured before the increment so this line joins to
        // the ones already written for it. Every other per-turn line — turn started, first audio,
        // token usage, the silence observations — reads the pre-increment value, so logging the
        // post-increment one here joined the start of turn N to the completion of turn N-1.
        var completedRound = _ctx.Round;

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

                if (idleFollowUp.OnTimeoutAsync != null)
                {
                    if (IsProviderSessionActive) 
                        await idleFollowUp.OnTimeoutAsync();
                    else 
                        Log.Warning("[RealtimeAi] Idle timeout action skipped, session no longer active, SessionId: {SessionId}", _ctx.SessionId);
                }
            });
        }

        await SendToClientAsync(_ctx.ClientAdapter.BuildTurnCompletedMessage(_ctx.SessionId)).ConfigureAwait(false);
        
        // Feeds the absolute turn ceiling the hardening plan adds later: without the real p99.9
        // that threshold would be a guess.
        Log.Information("[RealtimeAi] AI turn completed, SessionId: {SessionId}, Round: {Round}, TurnsCompleted: {TurnsCompleted}, ElapsedTurnMs: {ElapsedTurnMs}",
            _ctx.SessionId, completedRound, _ctx.Round, _ctx.TurnStartedAt == 0 ? 0 : (long)Stopwatch.GetElapsedTime(_ctx.TurnStartedAt).TotalMilliseconds);
    }

    private async Task ForwardProviderTextToTtsAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // First text of this external turn → it will wait for the TTS gate; arm the absolute hard
        // ceiling so the turn can never hang past it (covers a provider that streams text then stalls
        // without ever sending response.done). Exactly-once is guaranteed by the same handled latch.
        // Gated on external-TTS mode: in audio mode the turn completes on provider-done without waiting,
        // so a hard-ceiling watchdog must never arm there (defensive — audio mode emits no text today).
        // Also armed here, not only on response.created: a text-mode provider that streams without
        // announcing a response first would otherwise run with no ceiling at all.
        //
        // The external-TTS gate is load-bearing and was briefly dropped. The OpenAI adapter routes an
        // audio part's transcript to ResponseTextDone, so un-gated this armed a second ceiling on every
        // production audio turn — and it made a ceiling reachable on the Google path, whose adapter
        // never emits ResponseStarted and therefore never advances the turn generation, so one forced
        // completion would have stamped a generation that never changes again and silently swallowed
        // every subsequent turn completion for the rest of the call.
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

    /// <summary>
    /// Starts a new turn. Taken under the gate's own lock so the generation bump and the flag reset
    /// are one step: between them the gate would be readable with a new generation and the previous
    /// turn's flags, which is the state a late signal exploits.
    /// </summary>
    private async Task ResetCurrentResponseStateAsync()
    {
        await _ctx.TurnCompletionStateLock.WaitAsync(_ctx.SessionCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
        try
        {
            _ctx.TurnStartedAt = Stopwatch.GetTimestamp();
            _ctx.TurnFirstAudioReported = false;

            Interlocked.Increment(ref _ctx.CurrentTurnGeneration);

            _ctx.CurrentResponseHasTextOutput = false;
            _ctx.CurrentResponseTextDoneHandled = false;
            _ctx.CurrentResponseProviderTurnCompleted = false;
            _ctx.CurrentResponseTtsSynthesisCompleted = false;
            _ctx.CurrentResponseTurnCompletedHandled = false;
            _ctx.CurrentResponseTextBuilder.Clear();
        }
        finally
        {
            _ctx.TurnCompletionStateLock.Release();
        }
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

            // A watchdog already closed this turn. The provider recovering afterwards must not add a
            // second completion: Round would jump by two, and SkipRounds drives when the idle
            // follow-up and auto-hangup fire.
            if (Interlocked.Read(ref _ctx.ForceCompletedTurnGeneration) == Interlocked.Read(ref _ctx.CurrentTurnGeneration))
                return false;

            var completing = TryMarkCurrentResponseTurnCompletedLocked();

            if (completing) Interlocked.Exchange(ref _ctx.NormallyCompletedTurnGeneration, Interlocked.Read(ref _ctx.CurrentTurnGeneration));

            return completing;
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
        var repliesSent = 0;

        // Handlers run inline on the provider receive loop; the turn ceiling must not read that as the
        // provider having gone quiet. repeat_order alone packages the whole call as a WAV, uploads it
        // for a spoken readback and shells ffmpeg, uncancellable — one retried attempt outlives the
        // ceiling on a call that is working perfectly.
        _ctx.IsRunningFunctionCallHandlers = true;

        try
        {

        // Per handler, not per batch: this chain has seventeen tools doing POS lookups and HTTP
        // calls, and one of them failing must not discard its siblings' replies.
        foreach (var functionCall in functionCalls)
        {
            Log.Information("[RealtimeAi] Function call received, SessionId: {SessionId}, FunctionName: {FunctionName}", _ctx.SessionId, functionCall.FunctionName);

            var handlerStartedAt = Stopwatch.GetTimestamp();

            RealtimeAiFunctionCallResult result;

            try
            {
                result = await _ctx.Options.OnFunctionCallAsync(functionCall, _ctx.SessionActions).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Named against the tool that failed — "a function call failed" is not actionable
                // across seventeen of them.
                Log.Error(ex, "[RealtimeAi] Function call failed, SessionId: {SessionId}, FunctionName: {FunctionName}, ElapsedFunctionCallMs: {ElapsedFunctionCallMs}",
                    _ctx.SessionId, functionCall.FunctionName, (long)Stopwatch.GetElapsedTime(handlerStartedAt).TotalMilliseconds);

                if (await TryAnswerFailedFunctionCallAsync(functionCall).ConfigureAwait(false)) repliesSent++;

                continue;
            }

            // Handlers run inline on the provider receive loop, so this doubles as the measure of how
            // long that loop was blocked — and it is where the per-call timeout gets its value from.
            Log.Information("[RealtimeAi] Function call completed, SessionId: {SessionId}, FunctionName: {FunctionName}, ElapsedFunctionCallMs: {ElapsedFunctionCallMs}",
                _ctx.SessionId, functionCall.FunctionName, (long)Stopwatch.GetElapsedTime(handlerStartedAt).TotalMilliseconds);

            if (result?.ShouldTriggerResponse == true) shouldTriggerResponse = true;

            if (string.IsNullOrEmpty(result?.Output)) continue;

            // Sent as each handler finishes rather than accumulated, so a later failure cannot
            // discard a reply that was already produced.
            if (await TrySendFunctionCallReplyAsync(functionCall, result.Output).ConfigureAwait(false)) repliesSent++;
        }

        }
        finally
        {
            _ctx.IsRunningFunctionCallHandlers = false;
        }

        if (repliesSent > 0 || shouldTriggerResponse)
            await QueueOrTriggerProviderResponseAsync("function call").ConfigureAwait(false);
    }

    /// <summary>
    /// Sends one tool's reply, reporting failure instead of propagating it.
    ///
    /// <para>The send used to sit outside every catch, so a transport failure on one tool's reply
    /// unwound the whole batch and its remaining handlers never ran — the mirror image of the defect
    /// this loop's per-handler try was added to prevent, and on a real call the skipped sibling is
    /// hangup or transfer_call. Note the post-loop response trigger still rethrows on its own; this
    /// contains the send, not the whole method.</para>
    /// </summary>
    private async Task<bool> TrySendFunctionCallReplyAsync(RealtimeAiWssFunctionCallData functionCall, string output)
    {
        try
        {
            await SendToProviderAsync(_ctx.ProviderAdapter.BuildFunctionCallReplyMessage(functionCall, output)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Could not deliver a tool reply, SessionId: {SessionId}, FunctionName: {FunctionName}", _ctx.SessionId, functionCall.FunctionName);
            return false;
        }
    }

    /// <summary>
    /// Tells the model a tool failed, so it does not answer the customer from a call nobody replied to
    /// — a dangling tool call is how an assistant ends up confidently stating an order status it never
    /// received.
    ///
    /// <para>Bounded twice: once per tool, and at most a handful per session. Every answer completes a
    /// turn, and starting the idle timer stops it first, so the 60-second countdown restarts from zero
    /// each time; unbounded against a tool that keeps failing, the call never ends. Past the bound the
    /// engine falls back to sending nothing, which is what it did before, and the hangup happens as it
    /// does now.</para>
    ///
    /// <para>Silent without a call id: the reply has nothing to address, and a rejected message on a
    /// socket that has already closed is classified critical and drops the call.</para>
    /// </summary>
    private async Task<bool> TryAnswerFailedFunctionCallAsync(RealtimeAiWssFunctionCallData functionCall)
    {
        if (string.IsNullOrEmpty(functionCall.CallId)) return false;

        if (_ctx.FunctionCallFailureRepliesSent >= RealtimeAiFunctionCallReplyDefaults.MaxFailureRepliesPerSession) return false;

        if (!_ctx.FunctionCallFailuresAnswered.Add(functionCall.FunctionName)) return false;

        _ctx.FunctionCallFailureRepliesSent++;

        return await TrySendFunctionCallReplyAsync(functionCall, RealtimeAiFunctionCallReplyDefaults.FailureReplyOutput).ConfigureAwait(false);
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
        {
            // Recorded at the point of decision. Teardown cannot tell afterwards whether the socket
            // closed because the caller hung up or because the engine gave up on the provider.
            _ctx.TerminationCause ??= RealtimeAiSessionOutcome.ProviderFault;

            await DisconnectFromProviderAsync($"Critical provider error: {errorData.Message}").ConfigureAwait(false);
        }
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
