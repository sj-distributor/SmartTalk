using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    // Twilio plays G.711 µ-law at a fixed 8 kHz; the repeat-order audio must already be in that
    // codec because actions.SendAudioToClientAsync forwards the payload to the client untranscoded.
    private const int TwilioSampleRate = 8000;

    private async Task<RealtimeAiFunctionCallResult> ProcessRepeatOrderAsync(
        RealtimeAiSessionActions actions, CancellationToken cancellationToken)
    {
        actions.SuspendClientAudioToProvider();

        try
        {
            // Gray switch: BuildTtsConfig returns non-null only when MiniMax TTS is enabled for
            // this assistant (same gate that drives the live call's voice), so the repeat-order
            // voice matches the call. Disabled → keep the existing gpt-audio voice path.
            var ttsConfig = BuildTtsConfig(_ctx.Assistant);
            if (ttsConfig == null)
                await SendRepeatOrderHoldOnAudioAsync(actions).ConfigureAwait(false);

            var recordedAudio = await actions.GetRecordedAudioSnapshotAsync().ConfigureAwait(false);

            if (recordedAudio is { Length: > 0 })
            {
                var audioData = BinaryData.FromBytes(recordedAudio);

                if (ttsConfig != null)
                    await SendRepeatOrderWithMiniMaxAsync(audioData, ttsConfig, actions, cancellationToken).ConfigureAwait(false);
                else
                    await SendRepeatOrderWithOpenAiAsync(audioData, actions, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            actions.ResumeClientAudioToProvider();
        }

        return null;
    }

    /// <summary>
    /// Voices the repeat-order in the call's MiniMax voice: gpt-audio turns the recorded order into
    /// text (audio-in → text-out), then MiniMax streams that text back as audio so a long order starts
    /// playing immediately. Each chunk is converted to µ-law and pushed to the client as it arrives.
    ///
    /// <para>
    /// Fallback to the gpt-audio voice only happens when <b>no</b> audio has been streamed yet (empty
    /// text, or a failure during connect/handshake). Once any chunk has reached the caller, a later
    /// failure is logged but not replayed — re-running gpt-audio would make the customer hear the order
    /// twice.
    /// </para>
    /// </summary>
    private async Task SendRepeatOrderWithMiniMaxAsync(
        BinaryData audioData, RealtimeAiTtsConfig ttsConfig, RealtimeAiSessionActions actions, CancellationToken cancellationToken)
    {
        var anyAudioStreamed = false;

        try
        {
            var repeatText = await _openaiClient.GenerateTextChatCompletionFromAudioAsync(
                audioData, _ctx.Assistant.CustomRepeatOrderPrompt, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(repeatText))
            {
                Log.Warning("[AiAssistant] Repeat-order text empty, falling back to gpt-audio voice, CallSid: {CallSid}", _ctx.CallSid);
                await SendRepeatOrderWithOpenAiAsync(audioData, actions, cancellationToken).ConfigureAwait(false);
                return;
            }

            var sampleRate = ttsConfig.SampleRate ?? TwilioSampleRate;

            await _miniMaxTtsSynthesizer.SynthesizeStreamingAsync(ttsConfig, repeatText, async pcm16 =>
            {
                if (sampleRate != TwilioSampleRate)
                    pcm16 = AudioCodecConverter.Resample(pcm16, sampleRate, TwilioSampleRate);

                var uLawAudioBytes = AudioCodecConverter.Convert(pcm16, RealtimeAiAudioCodec.PCM16, RealtimeAiAudioCodec.MULAW);

                await actions.SendAudioToClientAsync(Convert.ToBase64String(uLawAudioBytes)).ConfigureAwait(false);

                anyAudioStreamed = true;
            }, cancellationToken).ConfigureAwait(false);

            if (!anyAudioStreamed)
            {
                Log.Warning("[AiAssistant] MiniMax repeat-order produced no audio, falling back to gpt-audio voice, CallSid: {CallSid}", _ctx.CallSid);
                await SendRepeatOrderWithOpenAiAsync(audioData, actions, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!anyAudioStreamed)
        {
            Log.Error(ex, "[AiAssistant] MiniMax repeat-order voice failed before any audio, falling back to gpt-audio voice, CallSid: {CallSid}", _ctx.CallSid);
            await SendRepeatOrderWithOpenAiAsync(audioData, actions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AiAssistant] MiniMax repeat-order voice failed mid-stream after partial audio, not replaying, CallSid: {CallSid}", _ctx.CallSid);
        }
    }

    /// <summary>
    /// Existing path: gpt-audio understands the recorded order and speaks the repeat in its own
    /// voice (audio-in → audio-out), converted to µ-law for Twilio.
    /// </summary>
    private async Task SendRepeatOrderWithOpenAiAsync(
        BinaryData audioData, RealtimeAiSessionActions actions, CancellationToken cancellationToken)
    {
        var responseAudio = await _openaiClient.GenerateAudioChatCompletionAsync(
            audioData,
            _ctx.Assistant.CustomRepeatOrderPrompt,
            _ctx.Assistant.ModelVoice,
            cancellationToken).ConfigureAwait(false);

        var uLawAudioBytes = await _ffmpegService.ConvertWavToULawAsync(responseAudio, cancellationToken).ConfigureAwait(false);

        await actions.SendAudioToClientAsync(Convert.ToBase64String(uLawAudioBytes)).ConfigureAwait(false);
    }

    private async Task SendRepeatOrderHoldOnAudioAsync(RealtimeAiSessionActions actions)
    {
        Enum.TryParse(_ctx.Assistant.ModelVoice, true, out AiSpeechAssistantVoice voice);
        voice = voice == default ? AiSpeechAssistantVoice.Alloy : voice;

        Enum.TryParse(_ctx.Assistant.ModelLanguage, true, out AiSpeechAssistantMainLanguage language);
        language = language == default ? AiSpeechAssistantMainLanguage.En : language;

        try
        {
            var stream = AudioHelper.GetRandomAudioStream(voice, language, "SmartTalk.Core.Assets.Audio.RepeatOrderHoldon");

            using var holdOnStream = new MemoryStream();
            await stream.CopyToAsync(holdOnStream).ConfigureAwait(false);
            var holdOn = Convert.ToBase64String(holdOnStream.ToArray());

            await actions.SendAudioToClientAsync(holdOn).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "[AiAssistant] Hold-on audio not found, Voice: {Voice}, Language: {Language}, skipping", voice, language);
        }
    }
}
