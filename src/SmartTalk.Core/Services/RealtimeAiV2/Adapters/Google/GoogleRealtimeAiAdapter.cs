using System.Text.Json;
using Serilog;
using SmartTalk.Core.Services.RealtimeAiV2.Services;
using SmartTalk.Core.Settings.Google;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Google;

public class GoogleRealtimeAiAdapter : IRealtimeAiProviderAdapter
{
    private readonly GoogleSettings _googleSettings;

    public GoogleRealtimeAiAdapter(GoogleSettings googleSettings)
    {
        _googleSettings = googleSettings;
    }

    public Dictionary<string, string> GetHeaders(RealtimeAiServerRegion region)
    {
        return new Dictionary<string, string>();
    }

    public object BuildSessionConfig(RealtimeSessionOptions options)
    {
        var modelConfig = options.ModelConfig;

        var sessionPayload = new
        {
            setup = new
            {
                model = modelConfig.ModelName,
                generationConfig = new {
                    temperature = 0.8,
                    responseModalities = new[] { "audio" },
                    speechConfig = new
                    {
                        languageCode = string.IsNullOrEmpty(modelConfig.ModelLanguage) ? "en-US" : modelConfig.ModelLanguage,
                        voiceConfig = new { prebuiltVoiceConfig = new { voiceName = string.IsNullOrEmpty(modelConfig.Voice) ? "Aoede" : modelConfig.Voice } }
                    }
                },
                systemInstruction = new
                {
                    parts = new[]
                    {
                        new { text = modelConfig.Prompt ?? string.Empty }
                    }
                },
                tools = modelConfig.Tools.Any() ? modelConfig.Tools : null,
                realtimeInputConfig = modelConfig.TurnDetection
            }
        };

        Log.Information("GoogleAdapter: 构建初始会话负载: {@Payload}", sessionPayload);

        return sessionPayload;
    }

    public string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData)
    {
        var mimeType = audioData.CustomProperties.GetValueOrDefault(nameof(RealtimeSessionOptions.InputFormat)) switch
        {
            RealtimeAiAudioCodec.PCM16 => "audio/pcm;rate=24000",
            _ => throw new NotSupportedException("mimeType")
        };

        var message = new
        {
            realtimeInput = new
            {
                audio = new { data = audioData.Base64Payload, mimeType = mimeType }
            }
        };
        var json = JsonSerializer.Serialize(message);
        return json;
    }
    
    public string BuildTextUserMessage(string text, string sessionId)
    {
        var message = new
        {
            clientContent = new
            {
                turnComplete = true,
                turns = new[] { new { parts = new[] { new { text } }, role = "user" } }
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
            Log.Information("GoogleAdapter: 构建打断消息，目标 item_id: {ItemId}", lastAssistantItemIdToInterrupt);
            return message;
        }
        Log.Warning("GoogleAdapter: 尝试构建打断消息但未提供 lastAssistantItemIdToInterrupt。");
        return null;
    }

    public string BuildTriggerResponseMessage()
    {
        return null;
    }

    public ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage)
    {
        Log.Information("Incoming response message: {@Msg}", rawMessage);
        
        try
        {
            using var jsonDocument = JsonDocument.Parse(rawMessage);
            var root = jsonDocument.RootElement;
            
            if (root.TryGetProperty("setupComplete", out var setupComplete))
                return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SessionInitialized, Data = rawMessage, RawJson = rawMessage };

            if (root.TryGetProperty("serverContent", out var serverContent))
            {
                if (serverContent.TryGetProperty("turnComplete", out var turnComplete) && bool.Parse(turnComplete.ToString()))
                {
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseTurnCompleted, RawJson = rawMessage };
                }

                if (serverContent.TryGetProperty("interrupted", out var interrupted) && bool.Parse(interrupted.ToString()))
                {
                    return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.SpeechDetected, RawJson = rawMessage };
                }

                if (serverContent.TryGetProperty("modelTurn", out var modelTurn) && modelTurn.TryGetProperty("parts", out var parts))
                {
                    if (parts[0].TryGetProperty("inlineData", out var inlineData) && inlineData.TryGetProperty("data", out var data))
                    {
                        return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.ResponseAudioDelta, Data = new RealtimeAiWssAudioData { Base64Payload = data.ToString() }, RawJson = rawMessage };
                    }

                    if (parts[0].TryGetProperty("text", out var text))
                    {
                        return new ParsedRealtimeAiProviderEvent {
                            Type = RealtimeAiWssEventType.ResponseTextDelta,
                            Data = null, //todo
                            RawJson = rawMessage
                        };
                    }
                }
            }
            
            Log.Warning("GoogleAdapter: 未知或未处理的 Google 返回格式");
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown, RawJson = rawMessage };
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, "GoogleAdapter: 解析 JSON 消息失败: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = "JSON 解析失败: " + jsonEx.Message, IsCritical = true }, RawJson = rawMessage };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GoogleAdapter: 解析消息时发生意外错误: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = "解析消息时发生意外错误: " + ex.Message, IsCritical = true }, RawJson = rawMessage };
        }
    }
    
    public AiSpeechAssistantProvider Provider => AiSpeechAssistantProvider.Google;
}