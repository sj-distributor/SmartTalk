using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Constants;
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
        var tools = BuildTools();

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
                Tools = tools,
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
            IdleFollowUp = BuildIdleFollowUp(),
            OnSessionReadyAsync = HandleSessionReadyAsync,
            OnClientStartAsync = HandleClientStartAsync,
            OnTranscriptionsCompletedAsync = HandleTranscriptionsCompletedAsync,
            OnRecordingCompleteAsync = HandleRecordingCompleteAsync,
            OnFunctionCallAsync = (data, actions) => OnFunctionCallAsync(data, actions, CancellationToken.None)
        };
    }

    private List<object> BuildTools()
    {
        var tools = _ctx.FunctionCalls
            .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool && !string.IsNullOrWhiteSpace(x.Content))
            .Select(x => JsonConvert.DeserializeObject<JObject>(x.Content))
            .Where(x => x != null)
            .Cast<object>()
            .ToList();

        if (_ctx.Assistant.ModelProvider == RealtimeAiProvider.OpenAi)
            EnsureGetProductPriceSchema(tools);

        return tools;
    }

    private static void EnsureGetProductPriceSchema(List<object> tools)
    {
        foreach (var tool in tools.OfType<JObject>())
        {
            var name = tool.Value<string>("name");
            if (!string.Equals(name, OpenAiToolConstants.GetProductPrice, StringComparison.OrdinalIgnoreCase))
                continue;

            tool["type"] ??= "function";

            var parameters = tool["parameters"] as JObject ?? new JObject();
            parameters["type"] ??= "object";

            var properties = parameters["properties"] as JObject ?? new JObject();
            if (properties["product_name"] == null)
            {
                properties["product_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "The product/dish name the user asked about."
                };
            }

            var required = parameters["required"] as JArray ?? new JArray();
            if (!required.Any(x => string.Equals(x?.ToString(), "product_name", StringComparison.Ordinal)))
                required.Add("product_name");

            parameters["properties"] = properties;
            parameters["required"] = required;
            parameters["additionalProperties"] ??= false;

            tool["parameters"] = parameters;
        }
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
}
