using System.Text.Json;
using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.OpenAi;

public class OpenAiRealtimeAiAdapter : IRealtimeAiProviderAdapter
{
    private readonly OpenAiSettings _openAiSettings;

    public OpenAiRealtimeAiAdapter(OpenAiSettings openAiSettings)
    {
        _openAiSettings = openAiSettings;
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

    public Task<object> GetInitialSessionPayloadAsync(
        RealtimeSessionOptions options, string sessionId, CancellationToken cancellationToken)
    {
        var modelConfig = options.ModelConfig;

        var sessionPayload = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = modelConfig.TurnDetection ?? new { type = "server_vad" },
                input_audio_format = options.InputFormat.GetDescription(),
                output_audio_format = options.OutputFormat.GetDescription(),
                voice = string.IsNullOrEmpty(modelConfig.Voice) ? "alloy" : modelConfig.Voice,
                instructions = modelConfig.Prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                input_audio_transcription = new { model = "whisper-1" },
                input_audio_noise_reduction = modelConfig.InputAudioNoiseReduction,
                tools = modelConfig.Tools.Any() ? modelConfig.Tools : null
            }
        };

        Log.Information("OpenAIAdapter: 构建初始会话负载: {@Payload}", sessionPayload);

        return Task.FromResult<object>(sessionPayload);
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
        if (!string.IsNullOrEmpty(lastAssistantItemIdToInterrupt))
        {
            var message = new
            {
                type = "conversation.interrupt",
                item_id_to_interrupt = lastAssistantItemIdToInterrupt
            };
            Log.Information("OpenAIAdapter: 构建打断消息，目标 item_id: {ItemId}", lastAssistantItemIdToInterrupt);
            return message;
        }
        Log.Warning("OpenAIAdapter: 尝试构建打断消息但未提供 lastAssistantItemIdToInterrupt。");
        return null;
    }

    public string BuildTriggerResponseMessage()
    {
        return JsonSerializer.Serialize(new { type = "response.create" });
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
    
    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.OpenAi;
}
