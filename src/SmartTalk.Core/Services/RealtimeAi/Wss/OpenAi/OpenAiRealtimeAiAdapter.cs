using System.Text.Json;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Wss;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAi.wss.OpenAi;

public class OpenAiRealtimeAiAdapter : IRealtimeAiProviderAdapter
{
    private readonly OpenAiSettings _openAiSettings;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public OpenAiRealtimeAiAdapter(OpenAiSettings openAiSettings, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _openAiSettings = openAiSettings;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region)
    {
        var apiKey = region == RealtimeAiServerRegion.US ? _openAiSettings.ApiKey : _openAiSettings.HkApiKey;
        
        return new Dictionary<string, string>
        {
            { "OpenAI-Beta", "realtime=v1" },
            { "Authorization", $"Bearer {apiKey}" }
        };
    }

    public async Task<object> GetInitialSessionPayloadAsync(
        Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile, RealtimeAiEngineContext context, string sessionId, CancellationToken cancellationToken)
    {
        var configs = await InitialSessionConfigAsync(assistantProfile, cancellationToken).ConfigureAwait(false);
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(assistantProfile.Id, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var sessionPayload = new
        {
            type = "session.update",
            session = new
            {
                audio = new
                {
                    input = new
                    {
                        transcription = new { model = "whisper-1" },
                        format = context.InputFormat.GetDescription(),
                        turn_detection = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.TurnDirection),
                        noise_reduction = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction),
                    },
                    output = new
                    {
                        format = context.OutputFormat.GetDescription(),
                        voice = string.IsNullOrEmpty(assistantProfile.ModelVoice) ? "alloy" : assistantProfile.ModelVoice,
                    }
                },
                instructions = knowledge?.Prompt ?? context.InitialPrompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.4,
                tools = configs.Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool).Select(x => x.Config)
            }
        };
        
        Log.Information("OpenAIAdapter: 构建初始会话负载: {@Payload}", sessionPayload);
        
        return sessionPayload;
    }

    public string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData)
    {
        var message = new
        {
            type = "input_audio_buffer.append",
            audio = audioData.Base64Payload
        };
        var json = JsonSerializer.Serialize(message);
        return json;
    }
    
    public string BuildTextUserMessage(string text, string sessionId)
    {
        var message = new
        {
            type = "conversation.item.create", // 假设的事件类型 (Assumed event type)
            item = new
            {
                type = "message",
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "input_text", text
                    }
                }
            }
        };
        return JsonSerializer.Serialize(message);
    }
    
    public object BuildInterruptMessage(string lastAssistantItemIdToInterrupt)
    {
        // ... (同前) (... (Same as before))
        if (!string.IsNullOrEmpty(lastAssistantItemIdToInterrupt))
        {
            var message = new
            {
                type = "conversation.interrupt", // 假设的事件类型 (Assumed event type)
                item_id_to_interrupt = lastAssistantItemIdToInterrupt
            };
            Log.Information("OpenAIAdapter: 构建打断消息，目标 item_id: {ItemId}", lastAssistantItemIdToInterrupt); // OpenAIAdapter: Building interrupt message, target item_id: {ItemId}
            return message; // 返回对象，由 Engine 序列化 (Return object, serialized by Engine)
        }
        Log.Warning("OpenAIAdapter: 尝试构建打断消息但未提供 lastAssistantItemIdToInterrupt。"); // OpenAIAdapter: Attempting to build interrupt message but lastAssistantItemIdToInterrupt was not provided.
        return null;
    }

    public ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(rawMessage);
            var root = jsonDocument.RootElement;
            var eventTypeString = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var itemId = root.TryGetProperty("item_id", out var itemIdProp) ? itemIdProp.GetString() : null;

            if (string.IsNullOrEmpty(eventTypeString))
            {
                Log.Warning("OpenAIAdapter: 消息中缺少 'type' 字段: {RawMessage}", rawMessage);
                return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown, RawJson = rawMessage };
            }
            
            switch (eventTypeString)
            {
                case "session.updated":
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized, Data = rawMessage, RawJson = rawMessage, ItemId = itemId };
                
                case "response.audio.delta":
                    var audioPayload = root.TryGetProperty("delta", out var deltaProp) ? deltaProp.GetString() : null;
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseAudioDelta, Data = new RealtimeAiWssAudioData { Base64Payload = audioPayload, ItemId = itemId }, RawJson = rawMessage, ItemId = itemId };
                
                case "response.audio.done":
                     return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseAudioDone, RawJson = rawMessage, ItemId = itemId };

                case "input_audio_buffer.speech_started":
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected, RawJson = rawMessage, ItemId = itemId };
                
                case "conversation.item.input_audio_transcription.delta":
                    var userTranscriptDelta = root.TryGetProperty("delta", out var delta) ? delta.GetString() : null;
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.InputAudioTranscriptionPartial, Data = new RealtimeAiWssTranscriptionData { Transcript = userTranscriptDelta, Speaker = AiSpeechAssistantSpeaker.User }, RawJson = rawMessage };
                
                case "conversation.item.input_audio_transcription.completed":
                    var userTranscript = root.TryGetProperty("transcript", out var transcript) ? transcript.GetString() : null;
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.InputAudioTranscriptionCompleted, Data = new RealtimeAiWssTranscriptionData { Transcript = userTranscript, Speaker = AiSpeechAssistantSpeaker.User }, RawJson = rawMessage };
                
                case "response.audio_transcript.delta":
                    var aiTranscriptDelta = root.TryGetProperty("delta", out var aiDelta) ? aiDelta.GetString() : null;
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.OutputAudioTranscriptionPartial, Data = new RealtimeAiWssTranscriptionData { Transcript = aiTranscriptDelta, Speaker = AiSpeechAssistantSpeaker.Ai }, RawJson = rawMessage };
                
                case "response.audio_transcript.done":
                    var aiTranscription = root.TryGetProperty("transcript", out var aiTranscript) ? aiTranscript.GetString() : null;
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, Data = new RealtimeAiWssTranscriptionData { Transcript = aiTranscription, Speaker = AiSpeechAssistantSpeaker.Ai }, RawJson = rawMessage };

                case "response.done":
                     return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted, RawJson = rawMessage, ItemId = itemId };

                case "error":
                    var errorMessage = "未知 OpenAI 错误";
                    if (root.TryGetProperty("error", out var errorProp) && errorProp.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString();
                    } else if (root.TryGetProperty("last_error", out var lastErrorProp) && lastErrorProp.ValueKind != JsonValueKind.Null && lastErrorProp.TryGetProperty("message", out var lastMsgProp)) {
                        errorMessage = lastMsgProp.GetString();
                    }
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new  { Message = errorMessage, IsCritical = true }, RawJson = rawMessage, ItemId = itemId };
                
                default:
                    Log.Information("OpenAIAdapter: 未知或未处理的 OpenAI 事件类型 '{EventTypeString}': {RawMessage}", eventTypeString, rawMessage);
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown, Data = eventTypeString, RawJson = rawMessage, ItemId = itemId };
            }
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, "OpenAIAdapter: 解析 JSON 消息失败: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = "JSON 解析失败: " + jsonEx.Message, IsCritical = true }, RawJson = rawMessage };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenAIAdapter: 解析消息时发生意外错误: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = "解析消息时发生意外错误: " + ex.Message, IsCritical = true }, RawJson = rawMessage };
        }
    }
    
    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync([assistant.Id], assistant.ModelProvider, true, cancellationToken).ConfigureAwait(false);

        return functions.Count == 0 ? [] : functions.Where(x => !string.IsNullOrWhiteSpace(x.Content)).Select(x => (x.Type, JsonConvert.DeserializeObject<object>(x.Content))).ToList();
    }

    private object InitialSessionParameters(List<(AiSpeechAssistantSessionConfigType Type, object Config)> configs, AiSpeechAssistantSessionConfigType type)
    {
        var config = configs.FirstOrDefault(x => x.Type == type);

        return type switch
        {
            AiSpeechAssistantSessionConfigType.TurnDirection => config.Config ?? new { type = "server_vad" },
            AiSpeechAssistantSessionConfigType.InputAudioNoiseReduction => config.Config,
            _ => throw new NotSupportedException(nameof(type))
        };
    }

    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.OpenAi;
}