using Newtonsoft.Json;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeSessionOptions BuildSessionOptions()
    {
        var timer = _ctx.Timer;
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
                InputAudioNoiseReduction = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction)
            },
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = _ctx.Assistant.Id.ToString()
            },
            WebSocket = _ctx.TwilioWebSocket,
            Region = RealtimeAiServerRegion.US,
            EnableRecording = true,
            IdleFollowUp = timer != null
                ? new RealtimeSessionIdleFollowUp
                {
                    TimeoutSeconds = timer.TimeSpanSeconds,
                    FollowUpMessage = timer.AlterContent,
                    SkipRounds = timer.SkipRound
                }
                : null,
            OnSessionReadyAsync = HandleSessionReadyAsync,
            OnClientStartAsync = HandleClientStartAsync,
            OnFunctionCallAsync = (data, actions) =>
                OnFunctionCallAsync(data, actions, CancellationToken.None),
            OnTranscriptionsCompletedAsync = HandleTranscriptionsCompletedAsync,
            OnRecordingCompleteAsync = HandleRecordingCompleteAsync
        };
    }

    private object DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType type)
    {
        var content = _ctx.FunctionCalls.FirstOrDefault(x => x.Type == type && !string.IsNullOrWhiteSpace(x.Content))?.Content;

        return content != null ? JsonConvert.DeserializeObject<object>(content) : null;
    }
}
