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

        // OpenAI removed the `OpenAI-Beta: realtime=v1` header from the Realtime API on 2026-05-07
        // when the API moved Beta → GA. Sending it now returns `invalid_beta` and the WS is closed
        // before the first session.update. Authorization is the only required custom header.
        return new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {apiKey}" }
        };
    }

    public object BuildSessionConfig(RealtimeSessionOptions options, RealtimeAiAudioCodec clientCodec)
    {
        var modelConfig = options.ModelConfig;

        // GA session.update payload (post 2026-05-07):
        // - new required `session.type` field ("realtime" | "transcription")
        // - audio config nested under `session.audio.{input,output}` rather than flat fields
        // - audio format becomes an object {type, rate} rather than a bare string
        // - `modalities` → `output_modalities`; `temperature` field dropped
        // Reference: https://platform.openai.com/docs/guides/realtime
        // Canonical sample: https://github.com/twilio-samples/speech-assistant-openai-realtime-api-python
        return new
        {
            type = "session.update",
            session = new
            {
                type = "realtime",
                instructions = modelConfig.Prompt,
                output_modalities = new[] { "audio" },
                audio = new
                {
                    input = new
                    {
                        format = ConvertCodecToGaFormat(clientCodec),
                        transcription = new { model = "whisper-1" },
                        turn_detection = modelConfig.TurnDetection ?? new { type = "server_vad" },
                        noise_reduction = modelConfig.InputAudioNoiseReduction
                    },
                    output = new
                    {
                        format = ConvertCodecToGaFormat(clientCodec),
                        voice = string.IsNullOrEmpty(modelConfig.Voice) ? "alloy" : modelConfig.Voice
                    }
                },
                tools = modelConfig.Tools.Any() ? modelConfig.Tools : null
            }
        };
    }

    /// <summary>
    /// Converts our internal codec enum to the GA session.update audio.format object.
    /// G.711 (pcmu/pcma) is fixed at 8 kHz in the GA contract; sending `rate` is
    /// rejected as an unknown field. PCM16 carries an explicit rate. Canonical reference:
    /// https://github.com/twilio-samples/speech-assistant-openai-realtime-api-python
    /// </summary>
    private static object ConvertCodecToGaFormat(RealtimeAiAudioCodec codec) => codec switch
    {
        RealtimeAiAudioCodec.MULAW => new { type = "audio/pcmu" },
        RealtimeAiAudioCodec.ALAW => new { type = "audio/pcma" },
        RealtimeAiAudioCodec.PCM16 => new { type = "audio/pcm", rate = 24000 },
        _ => new { type = "audio/pcmu" }
    };

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

                // GA renamed several audio events; accept BOTH the old beta name and the new GA
                // name so the deploy is robust to any leftover preview snapshots and to the GA
                // models. https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/realtime-audio-preview-api-migration-guide
                case "response.audio.delta":
                case "response.output_audio.delta":
                    var audioPayload = root.TryGetProperty("delta", out var deltaProp) ? deltaProp.GetString() : null;
                    return Result(RealtimeAiWssEventType.ResponseAudioDelta, new RealtimeAiWssAudioData { Base64Payload = audioPayload, ItemId = itemId });

                case "response.audio.done":
                case "response.output_audio.done":
                    return Result(RealtimeAiWssEventType.ResponseAudioDone);

                case "response.created":
                    return Result(RealtimeAiWssEventType.ResponseStarted);

                case "input_audio_buffer.speech_started":
                    return Result(RealtimeAiWssEventType.SpeechDetected);

                case "conversation.item.input_audio_transcription.delta":
                    return Result(RealtimeAiWssEventType.InputAudioTranscriptionPartial, Transcription("delta", AiSpeechAssistantSpeaker.User));

                case "conversation.item.input_audio_transcription.completed":
                    return Result(RealtimeAiWssEventType.InputAudioTranscriptionCompleted, Transcription("transcript", AiSpeechAssistantSpeaker.User));

                case "response.audio_transcript.delta":
                case "response.output_audio_transcript.delta":
                    return Result(RealtimeAiWssEventType.OutputAudioTranscriptionPartial, Transcription("delta", AiSpeechAssistantSpeaker.Ai));

                case "response.audio_transcript.done":
                case "response.output_audio_transcript.done":
                    return Result(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, Transcription("transcript", AiSpeechAssistantSpeaker.Ai));

                case "response.done":
                    var functionCalls = ExtractFunctionCalls(root);
                    return functionCalls != null ? Result(RealtimeAiWssEventType.FunctionCallSuggested, functionCalls) : Result(RealtimeAiWssEventType.ResponseTurnCompleted);

                case "error":
                    var errorCode = ExtractErrorCode(root);
                    var errorMessage = ExtractErrorMessage(root);
                    return Result(RealtimeAiWssEventType.Error, new RealtimeAiErrorData
                    {
                        Code = errorCode,
                        Message = errorMessage,
                        IsCritical = !IsRecoverableError(errorCode, errorMessage)
                    });

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

    private static string ExtractArgumentsJson(JsonElement item)
    {
        if (item.TryGetProperty("arguments", out var argsProp))
        {
            return argsProp.ValueKind switch
            {
                JsonValueKind.String => argsProp.GetString(),
                JsonValueKind.Undefined or JsonValueKind.Null => null,
                _ => argsProp.GetRawText()
            };
        }

        if (item.TryGetProperty("arguments_json", out var argsJsonProp))
        {
            return argsJsonProp.ValueKind switch
            {
                JsonValueKind.String => argsJsonProp.GetString(),
                JsonValueKind.Undefined or JsonValueKind.Null => null,
                _ => argsJsonProp.GetRawText()
            };
        }

        return null;
    }

    private static string ExtractErrorMessage(JsonElement root)
    {
        if (root.TryGetProperty("error", out var errorProp) && errorProp.TryGetProperty("message", out var msgProp))
            return msgProp.GetString();

        if (root.TryGetProperty("last_error", out var lastErrorProp) && lastErrorProp.ValueKind != JsonValueKind.Null && lastErrorProp.TryGetProperty("message", out var lastMsgProp))
            return lastMsgProp.GetString();

        return "Unknown OpenAI error";
    }

    private static string ExtractErrorCode(JsonElement root)
    {
        if (root.TryGetProperty("error", out var errorProp) && errorProp.TryGetProperty("code", out var codeProp))
            return codeProp.GetString();

        if (root.TryGetProperty("last_error", out var lastErrorProp) && lastErrorProp.ValueKind != JsonValueKind.Null && lastErrorProp.TryGetProperty("code", out var lastCodeProp))
            return lastCodeProp.GetString();

        return null;
    }

    private static bool IsRecoverableError(string errorCode, string errorMessage)
    {
        var normalizedCode = errorCode?.Trim();
        var normalizedMessage = errorMessage?.Trim();

        if (string.Equals(normalizedCode, "conversation_already_has_active_response", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(normalizedMessage) &&
            normalizedMessage.Contains("active response in progress", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
    
    public RealtimeAiAudioCodec GetPreferredCodec(RealtimeAiAudioCodec clientCodec) => clientCodec;
    
    public RealtimeAiProvider Provider => RealtimeAiProvider.OpenAi;
}
