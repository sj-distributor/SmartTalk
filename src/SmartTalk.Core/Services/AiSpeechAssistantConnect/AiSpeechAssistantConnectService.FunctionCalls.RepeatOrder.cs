using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private async Task<RealtimeAiFunctionCallResult> ProcessRepeatOrderAsync(
        RealtimeAiSessionActions actions, CancellationToken cancellationToken)
    {
        actions.SuspendClientAudioToProvider();

        try
        {
            await SendRepeatOrderHoldOnAudioAsync(actions).ConfigureAwait(false);

            var recordedAudio = await actions.GetRecordedAudioSnapshotAsync().ConfigureAwait(false);

            if (recordedAudio is { Length: > 0 })
            {
                var audioData = BinaryData.FromBytes(recordedAudio);

                var responseAudio = await _openaiClient.GenerateAudioChatCompletionAsync(
                    audioData,
                    _ctx.Assistant.CustomRepeatOrderPrompt,
                    _ctx.Assistant.ModelVoice,
                    cancellationToken).ConfigureAwait(false);

                var uLawAudioBytes = await _ffmpegService.ConvertWavToULawAsync(responseAudio, cancellationToken).ConfigureAwait(false);

                await actions.SendAudioToClientAsync(Convert.ToBase64String(uLawAudioBytes)).ConfigureAwait(false);
            }
        }
        finally
        {
            actions.ResumeClientAudioToProvider();
        }

        return null;
    }

    private async Task SendRepeatOrderHoldOnAudioAsync(RealtimeAiSessionActions actions)
    {
        Enum.TryParse(_ctx.Assistant.ModelVoice, true, out AiSpeechAssistantVoice voice);
        voice = voice == default ? AiSpeechAssistantVoice.Alloy : voice;

        Enum.TryParse(_ctx.Assistant.ModelLanguage, true, out AiSpeechAssistantMainLanguage language);
        language = language == default ? AiSpeechAssistantMainLanguage.En : language;

        try
        {
            var stream = AudioHelper.GetRandomAudioStream(voice, language, "hold_on");

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
