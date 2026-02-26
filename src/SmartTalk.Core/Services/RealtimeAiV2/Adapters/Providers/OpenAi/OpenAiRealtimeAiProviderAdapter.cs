using System.Text.Json;
using Serilog;
using SmartTalk.Core.Extensions;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;

public class OpenAiRealtimeAiProviderAdapter : IRealtimeAiProviderAdapter
{
    private readonly OpenAiSettings _openAiSettings;

    public OpenAiRealtimeAiProviderAdapter(OpenAiSettings openAiSettings)
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

    public object BuildSessionConfig(RealtimeSessionOptions options, RealtimeAiAudioCodec clientCodec)
    {
        var modelConfig = options.ModelConfig;

        var sessionPayload = new
        {
            type = "session.update",
            session = new
            {
                turn_detection = modelConfig.TurnDetection ?? new { type = "server_vad" },
                input_audio_format = clientCodec.GetDescription(),
                output_audio_format = clientCodec.GetDescription(),
                voice = string.IsNullOrEmpty(modelConfig.Voice) ? "alloy" : modelConfig.Voice,
                instructions = modelConfig.Prompt,
                modalities = new[] { "text", "audio" },
                temperature = 0.8,
                input_audio_transcription = new { model = "whisper-1" },
                input_audio_noise_reduction = modelConfig.InputAudioNoiseReduction,
                tools = modelConfig.Tools.Any() ? modelConfig.Tools : null
            }
        };

        return sessionPayload;
    }

    public string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData)
    {
        object message;
        
        var imageBase64 = audioData.CustomProperties.GetValueOrDefault("image") as string;

        if (!string.IsNullOrWhiteSpace(imageBase64))
        {
            message = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message", role = "user",
                    content = new object[]
                    {
                        new { type = "input_audio", audio = audioData.Base64Payload },
                        new { type = "input_image", image_url = $"data:image/jpeg;base64,{imageBase64}" }
                    }
                }
            };
        }
        else
        {
            message = new { type = "input_audio_buffer.append", audio = audioData.Base64Payload };
        }

        return JsonSerializer.Serialize(message);
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
            return message;
        }

        Log.Warning("[RealtimeAi] Cannot build interrupt message, missing item ID");
        return null;
    }

    public string BuildFunctionCallReplyMessage(RealtimeAiWssFunctionCallData functionCall, string output)
    {
        return JsonSerializer.Serialize(new
        {
            type = "conversation.item.create",
            item = new
            {
                type = "function_call_output",
                call_id = functionCall.CallId,
                output
            }
        });
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
            
            var eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var itemId = root.TryGetProperty("item_id", out var itemIdProp) ? itemIdProp.GetString() : null;

            if (string.IsNullOrEmpty(eventType))
            {
                Log.Warning("[RealtimeAi] Missing 'type' field in provider message");
                return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Unknown, RawJson = rawMessage };
            }

            ParsedRealtimeAiProviderEvent Result(RealtimeAiWssEventType type, object data = null) =>
                new() { Type = type, Data = data, RawJson = rawMessage, ItemId = itemId };

            RealtimeAiWssTranscriptionData Transcription(string jsonProp, AiSpeechAssistantSpeaker speaker) =>
                new() { Transcript = root.TryGetProperty(jsonProp, out var p) ? p.GetString() : null, Speaker = speaker };

            switch (eventType)
            {
                case "session.updated":
                    return Result(RealtimeAiWssEventType.SessionInitialized, rawMessage);

                case "response.audio.delta":
                    var audioPayload = root.TryGetProperty("delta", out var deltaProp) ? deltaProp.GetString() : null;
                    return Result(RealtimeAiWssEventType.ResponseAudioDelta, new RealtimeAiWssAudioData { Base64Payload = audioPayload, ItemId = itemId });

                case "response.audio.done":
                    return Result(RealtimeAiWssEventType.ResponseAudioDone);

                case "input_audio_buffer.speech_started":
                    return Result(RealtimeAiWssEventType.SpeechDetected);

                case "conversation.item.input_audio_transcription.delta":
                    return Result(RealtimeAiWssEventType.InputAudioTranscriptionPartial, Transcription("delta", AiSpeechAssistantSpeaker.User));

                case "conversation.item.input_audio_transcription.completed":
                    return Result(RealtimeAiWssEventType.InputAudioTranscriptionCompleted, Transcription("transcript", AiSpeechAssistantSpeaker.User));

                case "response.audio_transcript.delta":
                    return Result(RealtimeAiWssEventType.OutputAudioTranscriptionPartial, Transcription("delta", AiSpeechAssistantSpeaker.Ai));

                case "response.audio_transcript.done":
                    return Result(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, Transcription("transcript", AiSpeechAssistantSpeaker.Ai));

                case "response.done":
                    var functionCalls = ExtractFunctionCalls(root);
                    return functionCalls != null ? Result(RealtimeAiWssEventType.FunctionCallSuggested, functionCalls) : Result(RealtimeAiWssEventType.ResponseTurnCompleted);

                case "error":
                    return Result(RealtimeAiWssEventType.Error, new RealtimeAiErrorData { Message = ExtractErrorMessage(root), IsCritical = true });

                default:
                    Log.Debug("[RealtimeAi] Unhandled OpenAI event: {EventType}", eventType);
                    return Result(RealtimeAiWssEventType.Unknown, eventType);
            }
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, "[RealtimeAi] Failed to parse OpenAI message");
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = jsonEx.Message, IsCritical = true }, RawJson = rawMessage };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RealtimeAi] Unexpected error parsing OpenAI message");
            return new ParsedRealtimeAiProviderEvent { Type = RealtimeAiWssEventType.Error, Data = new RealtimeAiErrorData { Message = ex.Message, IsCritical = true }, RawJson = rawMessage };
        }
    }

    private static List<RealtimeAiWssFunctionCallData> ExtractFunctionCalls(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("output", out var output) ||
            output.GetArrayLength() == 0)
            return null;

        List<RealtimeAiWssFunctionCallData> results = null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "function_call")
                continue;

            var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var args = item.TryGetProperty("arguments", out var argsProp) ? argsProp.GetString() : null;

            if (string.IsNullOrEmpty(name)) continue;

            var callId = item.TryGetProperty("call_id", out var callIdProp) ? callIdProp.GetString() : null;

            results ??= new List<RealtimeAiWssFunctionCallData>();
            results.Add(new RealtimeAiWssFunctionCallData { CallId = callId, FunctionName = name, ArgumentsJson = args });
        }

        return results;
    }

    private static string ExtractErrorMessage(JsonElement root)
    {
        if (root.TryGetProperty("error", out var errorProp) && errorProp.TryGetProperty("message", out var msgProp))
            return msgProp.GetString();

        if (root.TryGetProperty("last_error", out var lastErrorProp) && lastErrorProp.ValueKind != JsonValueKind.Null && lastErrorProp.TryGetProperty("message", out var lastMsgProp))
            return lastMsgProp.GetString();

        return "Unknown OpenAI error";
    }
    
    public RealtimeAiAudioCodec GetPreferredCodec(RealtimeAiAudioCodec clientCodec) => clientCodec;
    
    public RealtimeAiProvider Provider => RealtimeAiProvider.OpenAi;
}
