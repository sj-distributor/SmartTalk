using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeSessionOptions BuildSessionOptions()
    {
        var assistant = _ctx.Assistant;

        return new RealtimeSessionOptions
        {
            ClientConfig = new RealtimeAiClientConfig
            {
                Client = RealtimeAiClient.Twilio
            },
            ModelConfig = new RealtimeAiModelConfig
            {
                Provider = assistant.ModelProvider,
                ServiceUrl = assistant.ModelUrl ?? AiSpeechAssistantStore.DefaultUrl,
                Voice = assistant.ModelVoice ?? "alloy",
                ModelName = assistant.ModelName,
                ModelLanguage = assistant.ModelLanguage,
                Prompt = _ctx.Prompt,
                Tools = _ctx.FunctionCalls
                    .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && !string.IsNullOrWhiteSpace(x.Content))
                    .Select(x => JsonConvert.DeserializeObject<object>(x.Content))
                    .ToList(),
                TurnDetection = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TurnDirection),
                InputAudioNoiseReduction = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction),
                TranscriptionLanguage = ParseTranscriptionLanguage(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TranscriptionLanguage))
            },
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = _ctx.Assistant.Id.ToString()
            },
            WebSocket = _ctx.TwilioWebSocket,
            Region = RealtimeAiServerRegion.US,
            EnableRecording = true,
            IdleFollowUp = BuildIdleFollowUp(),
            OnSessionReadyAsync = HandleSessionReadyAsync,
            OnClientStartAsync = HandleClientStartAsync,
            OnClientStopAsync = HandleClientStopAsync,
            OnSessionEndedAsync = HandleSessionEndedAsync,
            OnTranscriptionsCompletedAsync = HandleTranscriptionsCompletedAsync,
            OnRecordingCompleteAsync = HandleRecordingCompleteAsync,
            OnFunctionCallAsync = (data, actions) => OnFunctionCallAsync(data, actions, CancellationToken.None)
        };
    }

    private RealtimeSessionIdleFollowUp BuildIdleFollowUp()
    {
        const int defaultIdleTimeoutSeconds = 60;

        return new RealtimeSessionIdleFollowUp
        {
            SkipRounds = _ctx.Timer?.SkipRound,
            FollowUpMessage = _ctx.Timer?.AlterContent,
            TimeoutSeconds = _ctx.Timer?.TimeSpanSeconds ?? defaultIdleTimeoutSeconds,
            OnTimeoutAsync = _ctx.Timer == null ? DefaultIdleHandling : null
        };

        Task DefaultIdleHandling()
        {
            _backgroundJobClient.Schedule<IAiSpeechAssistantService>(x => x.HangupCallAsync(_ctx.CallSid, CancellationToken.None), TimeSpan.FromSeconds(2));
            
            return Task.CompletedTask;
        }
    }

    private object DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType type)
    {
        var content = _ctx.FunctionCalls.FirstOrDefault(x => x.Type == type && !string.IsNullOrWhiteSpace(x.Content))?.Content;

        return content != null ? JsonConvert.DeserializeObject<object>(content) : null;
    }

    /// <summary>
    /// Extracts the <c>language</c> string from a deserialised
    /// <see cref="AiSpeechAssistantSessionConfigType.TranscriptionLanguage"/> config object.
    /// Returns <c>null</c> for every shape that should leave the transcription payload
    /// byte-equivalent to the pre-hint behaviour: null input, non-JObject input,
    /// missing <c>language</c> property, or empty / whitespace value. This is the only
    /// place that interprets the JSON schema for the language hint — the adapter just
    /// reads the resulting nullable string.
    /// Public static so the schema-interpretation rules can be exhaustively unit tested.
    /// </summary>
    public static string ParseTranscriptionLanguage(object deserialisedConfig)
    {
        if (deserialisedConfig is not JObject obj) return null;

        var language = obj["language"]?.Value<string>();

        return string.IsNullOrWhiteSpace(language) ? null : language;
    }
}
