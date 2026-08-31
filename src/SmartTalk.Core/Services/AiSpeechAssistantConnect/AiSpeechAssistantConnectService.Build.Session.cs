using SmartTalk.Core.Domain.AISpeechAssistant;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Services.AiSpeechAssistantConnect;

public partial class AiSpeechAssistantConnectService
{
    private RealtimeSessionOptions BuildSessionOptions()
    {
        var assistant = _ctx.Assistant;
        var prompt = BuildSessionPrompt();

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
                Prompt = prompt,   // 代客致电: 有 instruction 则用作本通指令 (non-breaking)
                Tools = _ctx.FunctionCalls
                    .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && !string.IsNullOrWhiteSpace(x.Content))
                    .Select(CustomerItemsToolPrompt.DeserializeSessionToolContent)
                    .ToList(),
                TurnDetection = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TurnDirection),
                VendorOptions = new OpenAiRealtimeModelOptions
                {
                    InputAudioNoiseReduction = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction),
                    TranscriptionLanguage = ParseTranscriptionLanguage(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TranscriptionLanguage)),
                    TranscriptionModel = ParseTranscriptionModel(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TranscriptionModel)),
                    MaxResponseOutputTokens = ParseMaxResponseOutputTokens(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.MaxResponseOutputTokens)),
                    OutputAudioSpeed = ParseOutputAudioSpeed(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.OutputAudioSpeed)),
                    EnableRealtimeTracing = ParseEnableRealtimeTracing(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.RealtimeTracing))
                }
            },
            ConnectionProfile = new RealtimeAiConnectionProfile
            {
                ProfileId = _ctx.Assistant.Id.ToString()
            },
            TtsConfig = BuildTtsConfig(assistant),
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
            OnFunctionCallAsync = (data, actions) => OnFunctionCallAsync(data, actions, CancellationToken.None),
            OnResponseUsageReceivedAsync = HandleResponseUsageReceivedAsync
        };
    }

}
