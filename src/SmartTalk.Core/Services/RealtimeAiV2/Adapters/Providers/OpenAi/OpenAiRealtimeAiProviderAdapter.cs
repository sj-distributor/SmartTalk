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
    /// <summary>
    /// Compile-time default for the OpenAI transcription model. As of 2026-05-19
    /// <c>gpt-4o-transcribe</c> is OpenAI's most capable transcription model and is
    /// priced identically to the legacy <c>whisper-1</c> ($0.006/min), so this is a
    /// strict quality upgrade with no operating-cost change.
    /// <para>
    /// This is the ONE place to change when OpenAI ships a stronger default. Per-
    /// assistant override goes through <see cref="RealtimeAiModelConfig.TranscriptionModel"/>
    /// — operators who need to pin a specific assistant to <c>whisper-1</c> or to
    /// <c>gpt-4o-mini-transcribe</c> (cheaper) do so by inserting a
    /// <see cref="SmartTalk.Messages.Enums.AiSpeechAssistant.AiSpeechAssistantSessionConfigType.TranscriptionModel"/>
    /// row in <c>ai_speech_assistant_function_call</c>.
    /// </para>
    /// </summary>
    public const string DefaultTranscriptionModel = "gpt-4o-transcribe";

    /// <summary>
    /// Wire value emitted under <c>session.tracing</c> when an assistant opts into
    /// OpenAI's official session tracing. As of 2026-05-20 OpenAI documents <c>"auto"</c>
    /// as the no-config opt-in: OpenAI picks sensible defaults for workflow grouping
    /// and retention. Operators see the captured sessions at
    /// <c>https://platform.openai.com/traces</c>.
    /// </summary>
    public const string EnabledTracingMode = "auto";

    /// <summary>
    /// Compile-time default <c>audio.output.voice</c> when an assistant has no explicit
    /// <see cref="RealtimeAiModelConfig.Voice"/> override. Matches OpenAI's historical
    /// default and is one of the values in <see cref="SupportedVoices"/>.
    /// </summary>
    public const string DefaultVoice = "alloy";

    /// <summary>
    /// The voice IDs OpenAI's Realtime API accepts as of 2026-05-20. The adapter does
    /// NOT enforce this list — operators can opt into a future OpenAI voice without a
    /// code change, and OpenAI rejects unknown values server-side. The pin exists so
    /// (a) UI / operator tooling has a single source of truth for the dropdown and
    /// (b) a unit test asserts every entry stays in sync with the OpenAI docs.
    ///
    /// <para>
    /// Source: https://platform.openai.com/docs/guides/realtime — Voice Options.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedVoices = new[]
    {
        "alloy",
        "ash",
        "ballad",
        "coral",
        "echo",
        "fable",
        "onyx",
        "nova",
        "sage",
        "shimmer",
        "verse",
        "marin",
        "cedar"
    };

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
                // null for every assistant without a MaxResponseOutputTokens row; the
                // caller's NullValueHandling.Ignore (RealtimeAiService.Connect.cs:23)
                // strips the key entirely, so OpenAI uses its server-side default
                // (effectively unlimited within the session budget).
                max_response_output_tokens = modelConfig.MaxResponseOutputTokens,
                // null for every assistant without an opt-in RealtimeTracing row; the
                // serializer strips the key, so OpenAI does not retain a trace
                // (current behaviour). Setting EnableRealtimeTracing = true emits
                // `tracing = "auto"` and the session shows up in the OpenAI
                // traces dashboard for 30 days — escalation-debug tool.
                tracing = modelConfig.EnableRealtimeTracing == true ? EnabledTracingMode : null,
                audio = new
                {
                    input = new
                    {
                        format = ConvertCodecToGaFormat(clientCodec),
                        // model defaults to DefaultTranscriptionModel (currently gpt-4o-transcribe);
                        // operators downgrade specific assistants to whisper-1 or gpt-4o-mini-transcribe
                        // by populating a TranscriptionModel row in ai_speech_assistant_function_call.
                        //
                        // language is null when no TranscriptionLanguage row exists for the assistant;
                        // the caller's NullValueHandling.Ignore (RealtimeAiService.Connect.cs:23) strips
                        // the key entirely, keeping the transcription object byte-equivalent to
                        // `{ model: <default> }` for every assistant without the language hint.
                        transcription = new
                        {
                            // IsNullOrWhiteSpace (not IsNullOrEmpty) so an accidental " " or "\t"
                            // in the config row also falls back to the adapter default rather than
                            // being forwarded as an invalid model literal and rejected by OpenAI.
                            model = string.IsNullOrWhiteSpace(modelConfig.TranscriptionModel) ? DefaultTranscriptionModel : modelConfig.TranscriptionModel,
                            language = modelConfig.TranscriptionLanguage
                        },
                        turn_detection = modelConfig.TurnDetection ?? new { type = "server_vad" },
                        noise_reduction = modelConfig.InputAudioNoiseReduction
                    },
                    output = new
                    {
                        format = ConvertCodecToGaFormat(clientCodec),
                        // Single source of truth for the default; SupportedVoices documents
                        // the full operator-selectable list. The adapter does NOT validate
                        // against the list — operators can opt into a future OpenAI voice
                        // without a code change, and OpenAI rejects unknown ones server-side.
                        voice = string.IsNullOrEmpty(modelConfig.Voice) ? DefaultVoice : modelConfig.Voice,
                        // null for every assistant without an OutputAudioSpeed row; the
                        // caller's NullValueHandling.Ignore (RealtimeAiService.Connect.cs:23)
                        // strips the key entirely, so OpenAI uses its default 1.0.
                        speed = modelConfig.OutputAudioSpeed
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
    
    public string BuildTruncateMessage(string itemId, long audioEndMs)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Log.Warning("[RealtimeAi] Cannot build truncate message, missing item ID");
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            type = "conversation.item.truncate",
            item_id = itemId,
            content_index = 0,
            audio_end_ms = Math.Max(0L, audioEndMs)
        });
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

                // Beta-era event names (`response.audio.delta`, `response.audio.done`,
                // `response.audio_transcript.delta`, `response.audio_transcript.done`) were
                // accepted alongside the GA names during the hotfix #934 transition window.
                // OpenAI completed the GA cutover on 2026-05-07 and has not emitted Beta names
                // in production logs for ≥ 2 weeks; the dual-case branches are removed here so
                // a Beta-named event surfaces as Unknown (= operator-visible Serilog warning)
                // rather than silently working — that surface signal is required to detect any
                // future provider regression that would re-emit Beta names.
                case "response.output_audio.delta":
                    var audioPayload = root.TryGetProperty("delta", out var deltaProp) ? deltaProp.GetString() : null;
                    return Result(RealtimeAiWssEventType.ResponseAudioDelta, new RealtimeAiWssAudioData { Base64Payload = audioPayload, ItemId = itemId });

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

                case "response.output_audio_transcript.delta":
                    return Result(RealtimeAiWssEventType.OutputAudioTranscriptionPartial, Transcription("delta", AiSpeechAssistantSpeaker.Ai));

                case "response.output_audio_transcript.done":
                    return Result(RealtimeAiWssEventType.OutputAudioTranscriptionCompleted, Transcription("transcript", AiSpeechAssistantSpeaker.Ai));

                case "response.done":
                    var functionCalls = ExtractFunctionCalls(root);
                    // Usage is attached to BOTH event variants because both originate from
                    // the same provider message. Consumers handle the variant they care
                    // about and read Usage off the event independently.
                    var usage = ExtractUsage(root);
                    var doneEvent = functionCalls != null
                        ? Result(RealtimeAiWssEventType.FunctionCallSuggested, functionCalls)
                        : Result(RealtimeAiWssEventType.ResponseTurnCompleted);
                    doneEvent.Usage = usage;
                    return doneEvent;

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
            var args = ExtractArgumentsJson(item);

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

    /// <summary>
    /// Extracts the OpenAI token-usage breakdown from a <c>response.done</c> payload
    /// (shape: <c>{ response: { usage: { input_tokens, output_tokens, total_tokens,
    /// input_token_details: { cached_tokens, audio_tokens, text_tokens },
    /// output_token_details: { audio_tokens, text_tokens } } } }</c>).
    ///
    /// <para>
    /// Returns <c>null</c> when the message has no usage block (older provider snapshots),
    /// or when the response object is missing. Individual sub-fields are returned as
    /// <c>null</c> when absent — consumers must treat missing values as "not reported"
    /// rather than zero, because zero is a meaningful answer (an empty AI turn).
    /// </para>
    ///
    /// <para>
    /// Public static (matches the other parser helpers in this codebase) so unit
    /// tests can pin the schema interpretation directly without driving a full
    /// ParseMessage path.
    /// </para>
    /// </summary>
    public static RealtimeAiWssUsageData ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var response) ||
            response.ValueKind != JsonValueKind.Object ||
            !response.TryGetProperty("usage", out var usage) ||
            usage.ValueKind != JsonValueKind.Object)
            return null;

        return new RealtimeAiWssUsageData
        {
            TotalTokens = ReadOptionalInt(usage, "total_tokens"),
            InputTokens = ReadOptionalInt(usage, "input_tokens"),
            OutputTokens = ReadOptionalInt(usage, "output_tokens"),
            CachedTokens = ReadOptionalInt(usage, "input_token_details", "cached_tokens"),
            InputAudioTokens = ReadOptionalInt(usage, "input_token_details", "audio_tokens"),
            InputTextTokens = ReadOptionalInt(usage, "input_token_details", "text_tokens"),
            OutputAudioTokens = ReadOptionalInt(usage, "output_token_details", "audio_tokens"),
            OutputTextTokens = ReadOptionalInt(usage, "output_token_details", "text_tokens")
        };
    }

    /// <summary>
    /// Reads a nullable integer at <paramref name="path"/> from <paramref name="parent"/>.
    /// Returns null if any segment is missing, not an object on the way down, or
    /// the final value is not a JSON number / integer-compatible value. Tolerates
    /// the provider returning long / double for what we treat as int.
    /// </summary>
    private static int? ReadOptionalInt(JsonElement parent, params string[] path)
    {
        var current = parent;

        for (var i = 0; i < path.Length - 1; i++)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(path[i], out var next))
                return null;

            current = next;
        }

        if (current.ValueKind != JsonValueKind.Object ||
            !current.TryGetProperty(path[^1], out var leaf) ||
            leaf.ValueKind != JsonValueKind.Number)
            return null;

        return leaf.TryGetInt32(out var i32) ? i32 : (int?)null;
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
