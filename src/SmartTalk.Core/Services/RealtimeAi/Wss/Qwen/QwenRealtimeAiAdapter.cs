using System.Text.Json;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Core.Services.RealtimeAi.wss;
using SmartTalk.Core.Settings.Qwen;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = Newtonsoft.Json.JsonException;

namespace SmartTalk.Core.Services.RealtimeAi.Wss.Qwen;

public class QwenRealtimeAiAdapter : IRealtimeAiProviderAdapter
{
    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.Qwen;

    private readonly QwenSettings _qwenSettings;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;

    public QwenRealtimeAiAdapter(QwenSettings qwenSettings, IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _qwenSettings = qwenSettings;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
    }

    public Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region)
    {
        return new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_qwenSettings.ApiKey}" }
        };
    }

    public async Task<object> GetInitialSessionPayloadAsync(
        RealtimeSessionOptions options, string sessionId = null, CancellationToken cancellationToken = default)
    {
        var configs = await InitialSessionConfigAsync(options, cancellationToken).ConfigureAwait(false);
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(int.Parse(options.ConnectionProfile.ProfileId), isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var turnDetection = InitialSessionParameters(configs, AiSpeechAssistantSessionConfigType.TurnDirection);

        var sessionPayload = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = turnDetection,
                input_audio_format = options.InputFormat.GetDescription(),
                output_audio_format = options.OutputFormat.GetDescription(),
                voice = string.IsNullOrEmpty(options.ModelConfig.Voice) ? "alloy" : options.ModelConfig.Voice,
                instructions = knowledge?.Prompt ?? options.InitialPrompt,
                modalities = turnDetection == null ? new[] { "text" } : new[] { "text", "audio" },
                temperature = 0.8
            }
        };
        
        Log.Information("Qwen Adapter: 构建初始会话负载: {@Payload}", sessionPayload);
        
        return sessionPayload;
    }

    (string MessageJson, bool IsOpenAiImage) IRealtimeAiProviderAdapter.BuildAudioAppendMessage(RealtimeAiWssAudioData audioData)
    {
        var imageBase64 = audioData.CustomProperties.GetValueOrDefault("image") as string;
        var hasAudio = !string.IsNullOrWhiteSpace(audioData.Base64Payload);

        if (!hasAudio)
            return (null, false);

        if (!string.IsNullOrWhiteSpace(imageBase64))
        {
            var mergedMessage = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_audio", audio = audioData.Base64Payload },
                        new { type = "input_image", image_url = $"data:image/jpeg;base64,{imageBase64}" }
                    }
                }
            };
            return (JsonConvert.SerializeObject(mergedMessage), true);
        }
        else
        {
            var audioMessage = new
            {
                type = "input_audio_buffer.append",
                audio = audioData.Base64Payload
            };
            return (JsonConvert.SerializeObject(audioMessage), false);
        }
    }

    public string BuildCommitAudioMessage()
    {
        var commitAudioMessage = new
        {
            type = "input_audio_buffer.commit"
        };
        
        return JsonConvert.SerializeObject(commitAudioMessage);
    }

    public string BuildTextUserMessage(string text, string sessionId)
    {
        var message = new
        {
            type = "conversation.item.create",
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
        
        return JsonConvert.SerializeObject(message);
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
            Log.Information("Qwen Adapter: 构建打断消息，目标 item_id: {ItemId}", lastAssistantItemIdToInterrupt);
            return message;
        }
        Log.Warning("Qwen Adapter: 尝试构建打断消息但未提供 lastAssistantItemIdToInterrupt。"); 
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
                Log.Warning("Qwen Adapter: 消息中缺少 'type' 字段: {RawMessage}", rawMessage);
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
                    var errorMessage = "未知 Qwen 错误";
                    if (root.TryGetProperty("error", out var errorProp) && errorProp.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString();
                    } else if (root.TryGetProperty("last_error", out var lastErrorProp) && lastErrorProp.ValueKind != JsonValueKind.Null && lastErrorProp.TryGetProperty("message", out var lastMsgProp)) {
                        errorMessage = lastMsgProp.GetString();
                    }
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new  { Message = errorMessage, IsCritical = true }, RawJson = rawMessage, ItemId = itemId };
                
                default:
                    Log.Information("Qwen Adapter: 未知或未处理的 Qwen 事件类型 '{EventTypeString}': {RawMessage}", eventTypeString, rawMessage);
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown, Data = eventTypeString, RawJson = rawMessage, ItemId = itemId };
            }
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, "Qwen Adapter: 解析 JSON 消息失败: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = "JSON 解析失败: " + jsonEx.Message, IsCritical = true }, RawJson = rawMessage };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Qwen Adapter: 解析消息时发生意外错误: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = "解析消息时发生意外错误: " + ex.Message, IsCritical = true }, RawJson = rawMessage };
        }
    }
    
    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(RealtimeSessionOptions options, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdsAsync([int.Parse(options.ConnectionProfile.ProfileId)], options.ModelConfig.Provider, true, cancellationToken).ConfigureAwait(false);

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
}