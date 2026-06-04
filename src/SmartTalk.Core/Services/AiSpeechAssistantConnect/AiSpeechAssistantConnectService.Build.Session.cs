using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Constants;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.RealtimeAi;
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
                Tools = BuildTools(),
                TurnDetection = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TurnDirection),
                InputAudioNoiseReduction = DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction),
                TranscriptionLanguage = ParseTranscriptionLanguage(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TranscriptionLanguage)),
                TranscriptionModel = ParseTranscriptionModel(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.TranscriptionModel)),
                MaxResponseOutputTokens = ParseMaxResponseOutputTokens(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.MaxResponseOutputTokens)),
                OutputAudioSpeed = ParseOutputAudioSpeed(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.OutputAudioSpeed)),
                EnableRealtimeTracing = ParseEnableRealtimeTracing(DeserializeFunctionCallConfig(AiSpeechAssistantSessionConfigType.RealtimeTracing))
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
            OnFunctionCallAsync = (data, actions) => OnFunctionCallAsync(data, actions, CancellationToken.None),
            OnResponseUsageReceivedAsync = HandleResponseUsageReceivedAsync
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
            properties["product_name"] ??= new JObject
            {
                ["type"] = "string",
                ["description"] = "The product name the customer asked about."
            };

            properties["customer_hint"] ??= new JObject
            {
                ["type"] = "string",
                ["description"] =
                    "Any customer-identifying detail explicitly mentioned by the customer, such as restaurant/customer name, street address, warehouse number, header note/remark, contact name, contact role, or restaurant/store related clue. Do not guess; only include details the customer actually said."
            };

            var required = parameters["required"] as JArray ?? new JArray();
            if (!required.Any(x => string.Equals(x?.ToString(), "product_name", StringComparison.Ordinal)))
                required.Add("product_name");

            parameters["properties"] = properties;
            parameters["required"] = required;
            parameters["additionalProperties"] ??= false;

            tool["parameters"] = parameters;
        }
    }
    
    /// <summary>
    /// Logs per-turn OpenAI token usage with assistant + call context so cost reports
    /// can be reconstructed from structured Serilog properties. Intentionally fire-and-
    /// forget — never throws to the realtime event loop. Future work can route the same
    /// payload to a cost-tracking sink (e.g. AiSpeechAssistantCallReport.TokensUsed)
    /// without touching the adapter side.
    /// </summary>
    private Task HandleResponseUsageReceivedAsync(RealtimeAiWssUsageData usage)
    {
        Log.Information(
            "[AiAssistant] Token usage, AssistantId: {AssistantId}, CallSid: {CallSid}, " +
            "Total: {Total}, Input: {Input}, Output: {Output}, Cached: {Cached}, " +
            "InputAudio: {InputAudio}, InputText: {InputText}, OutputAudio: {OutputAudio}, OutputText: {OutputText}",
            _ctx.Assistant?.Id, _ctx.CallSid,
            usage.TotalTokens, usage.InputTokens, usage.OutputTokens, usage.CachedTokens,
            usage.InputAudioTokens, usage.InputTextTokens, usage.OutputAudioTokens, usage.OutputTextTokens);

        return Task.CompletedTask;
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

    /// <summary>
    /// Extracts the <c>model</c> string from a deserialised
    /// <see cref="AiSpeechAssistantSessionConfigType.TranscriptionModel"/> config object.
    /// Returns <c>null</c> for every shape that should leave the adapter on its
    /// compile-time default: null input, non-JObject input, missing <c>model</c>
    /// property, or empty / whitespace value. The string itself is NOT validated
    /// against a known list — operators may opt into future OpenAI models without
    /// a code change, and OpenAI rejects unknown values server-side rather than us
    /// silently falling back.
    /// Public static so the schema-interpretation rules can be exhaustively unit tested.
    /// </summary>
    public static string ParseTranscriptionModel(object deserialisedConfig)
    {
        if (deserialisedConfig is not JObject obj) return null;

        var model = obj["model"]?.Value<string>();

        return string.IsNullOrWhiteSpace(model) ? null : model;
    }

    /// <summary>
    /// Extracts the <c>value</c> integer from a deserialised
    /// <see cref="AiSpeechAssistantSessionConfigType.MaxResponseOutputTokens"/> config object.
    /// Returns <c>null</c> for every shape that should leave the response cap unset
    /// (so OpenAI uses its server-side default): null input, non-JObject input,
    /// missing <c>value</c> property, non-integer value, or non-positive integer.
    /// Non-positive caps are rejected here rather than passed through because they
    /// would either be rejected by OpenAI server-side anyway (zero / negative) or
    /// silently truncate AI responses to an empty turn (one), which is worse than
    /// the default.
    /// Public static so the schema-interpretation rules can be exhaustively unit tested.
    /// </summary>
    public static int? ParseMaxResponseOutputTokens(object deserialisedConfig)
    {
        if (deserialisedConfig is not JObject obj) return null;

        var token = obj["value"];

        if (token == null || token.Type != JTokenType.Integer) return null;

        var value = token.Value<int>();

        return value > 0 ? value : null;
    }

    /// <summary>
    /// Extracts the <c>value</c> decimal from a deserialised
    /// <see cref="AiSpeechAssistantSessionConfigType.OutputAudioSpeed"/> config object.
    /// Returns <c>null</c> for every shape that should leave the speed unset (so
    /// OpenAI uses 1.0, current behaviour): null input, non-JObject input, missing
    /// <c>value</c> property, non-numeric value, or non-positive value. The parser
    /// does NOT enforce OpenAI's range (currently 0.25 – 1.5) — operators who set
    /// out-of-range values are rejected by OpenAI server-side with a clear error,
    /// rather than the adapter silently clamping into a different speed than the
    /// operator intended.
    /// Public static so the schema-interpretation rules can be exhaustively unit tested.
    /// </summary>
    public static decimal? ParseOutputAudioSpeed(object deserialisedConfig)
    {
        if (deserialisedConfig is not JObject obj) return null;

        var token = obj["value"];

        if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)) return null;

        var value = token.Value<decimal>();

        return value > 0 ? value : null;
    }

    /// <summary>
    /// Extracts the <c>enabled</c> boolean from a deserialised
    /// <see cref="AiSpeechAssistantSessionConfigType.RealtimeTracing"/> config object.
    /// Returns <c>null</c> for every shape that should leave tracing off (current
    /// behaviour): null input, non-JObject input, missing <c>enabled</c> property,
    /// non-boolean value, or explicit <c>false</c>. Only an explicit <c>true</c>
    /// activates tracing; the parser distinguishes <c>false</c> from <c>null</c> so
    /// an operator can persist an explicit "off" state alongside the inactive flag.
    /// Public static so the schema-interpretation rules can be exhaustively unit tested.
    /// </summary>
    public static bool? ParseEnableRealtimeTracing(object deserialisedConfig)
    {
        if (deserialisedConfig is not JObject obj) return null;

        var token = obj["enabled"];

        if (token == null || token.Type != JTokenType.Boolean) return null;

        return token.Value<bool>() ? true : null;
    }
}
