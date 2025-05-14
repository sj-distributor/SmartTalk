using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.wss.Google;

public class GoogleRealtimeAiAdapter : IRealtimeAiProviderAdapter
{
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private static readonly JsonSerializerSettings _newtonsoftJsonSettings = new JsonSerializerSettings
    {
        ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    public GoogleRealtimeAiAdapter(IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider)
    {
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider
            ?? throw new ArgumentNullException(nameof(aiSpeechAssistantDataProvider));
    }

    public async Task<object> GetInitialSessionPayloadAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistantProfile, string initialUserPrompt = null, string sessionId = null, CancellationToken cancellationToken = default)
    {
        var configs = await InitialSessionConfigAsync(assistantProfile, cancellationToken).ConfigureAwait(false);
        var knowledge = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantKnowledgeAsync(assistantProfile.Id, isActive: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        string systemInstructionString = knowledge?.Prompt ?? string.Empty;

        var speechConfigObject = new
        {
            // languageCode is optional, add if there is a relevant configuration in assistantProfile
            languageCode = (string)null,

            voiceConfig = new // Inline voiceConfig directly
            {
                prebuiltVoiceConfig = new // Inline prebuiltVoiceConfig directly
                {
                    // Use assistantProfile.ModelVoice, if empty, provide a default WaveNet voice name
                    // Example: en-US-Wavenet-A (American English, WaveNet female voice) is a high-quality common option
                    // You can change this default as needed, or ensure assistantProfile.ModelVoice always provides a valid WaveNet voiceName
                    voiceName = string.IsNullOrWhiteSpace(assistantProfile.ModelVoice)
                        ? "en-US-Wavenet-A" // Default to an English WaveNet voice
                        : assistantProfile.ModelVoice
                }
                // If the VoiceConfig union type has other options (e.g., customVoiceConfig),
                // you need to choose which field to populate based on conditions.
            }
        };

        // 4. Build generationConfig
        var generationConfigObject = new
        {
            // candidateCount = 1,
            // maxOutputTokens = 2048,
            // temperature = 0.7,
            // ... other generation parameters ...
            responseModalities = new[] { "TEXT", "AUDIO" },
            speechConfig = speechConfigObject // Use the updated speechConfigObject
            // mediaResolution = ..., // If needed
        };

        // 4. Build tools (similar to previous logic, ensure it conforms to the [object] format)
        // Usually [{ "functionDeclarations": [...] }]
        object toolsObject = null;
        if (configs.Any(x => x.Type == AiSpeechAssistantSessionConfigType.Tool))
        {
            toolsObject = new[]
            {
                new {
                    functionDeclarations = configs
                        .Where(x => x.Type == AiSpeechAssistantSessionConfigType.Tool)
                        .Select(x => x.Config)
                        .ToList()
                }
            };
        }

        // 5. Build the final initial session payload
        var sessionPayload = new
        {
            // model field is required, get it from assistantProfile or other configurations
            model = "gemini-1.5-pro-latest", // Ensure assistantProfile has a ModelId property or similar configuration
            generationConfig = generationConfigObject,
            systemInstruction = systemInstructionString, // Directly the string
            tools = toolsObject
        };

        Log.Information("GoogleAdapter: Building initial session payload: {Payload}", JsonConvert.SerializeObject(sessionPayload, _newtonsoftJsonSettings));
        return sessionPayload;
    }

    public string BuildAudioAppendMessage(RealtimeAiWssAudioData audioData)
    {
        if (audioData == null || string.IsNullOrWhiteSpace(audioData.Base64Payload))
        {
            Log.Warning("GoogleAdapter BuildAudioAppendMessage: AudioData or its Base64Payload is empty.");
            return null;
        }

        var realtimeInputMessage = new
        {
            realtimeInput = new
            {
                audioBlob = audioData.Base64Payload
                // item_id and other metadata do not seem to be directly defined under realtimeInput.audioBlob
            }
        };
        return JsonConvert.SerializeObject(realtimeInputMessage, _newtonsoftJsonSettings);
    }

    public string BuildTextUserMessage(string text, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Log.Warning("GoogleAdapter BuildTextUserMessage: Text is empty.");
            return null;
        }

        var realtimeInputMessage = new
        {
            realtimeInput = new
            {
                text = text,
                session_id = sessionId
                // The handling of sessionId needs to be determined based on the API design.
                // It might be handled in the setup message, or as other top-level parameters (if the API allows).
                // If it needs to be passed in each message, it might need to be at the same level as realtimeInput, but this does not conform to the current specification.
            }
        };
        return JsonConvert.SerializeObject(realtimeInputMessage, _newtonsoftJsonSettings);
    }

    public ParsedRealtimeAiProviderEvent ParseMessage(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            Log.Warning("GoogleAdapter: Received raw message is empty.");
            return new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.Error,
                RawJson = rawMessage,
                Data = new RealtimeAiErrorData
                    { Message = "Raw message is empty or whitespace", IsCritical = false, Code = "EMPTY_MESSAGE" }
            };
        }

        var parsedEvent = new ParsedRealtimeAiProviderEvent { RawJson = rawMessage };

        try
        {
            JObject jObject = JObject.Parse(rawMessage);

            if (jObject["usageMetadata"] is JObject usageMetadataObj)
            {
                parsedEvent.Type = RealtimeAiWssEventType.Unknown;
                parsedEvent.Data = usageMetadataObj.ToObject<object>();
                Log.Information("GoogleAdapter: Received Usage Metadata: {UsageMetadataJson}", usageMetadataObj.ToString(Formatting.None));
            }
            else if (jObject["setupComplete"] is JObject setupCompleteObj)
            {
                parsedEvent.Type = RealtimeAiWssEventType.SessionInitialized;
                Log.Information("GoogleAdapter: Session setup complete.");
            }
            else if (jObject["serverContent"] is JObject serverContentObj)
            {
                ParseServerContentIntoEvent(serverContentObj, parsedEvent);
            }
            else if (jObject["toolCall"] is JObject toolCallObj)
            {
                parsedEvent.Type = RealtimeAiWssEventType.FunctionCallSuggested;
                JArray functionCalls = toolCallObj["functionCalls"] as JArray;
                if (functionCalls != null && functionCalls.Count > 0)
                {
                    List<RealtimeAiWssFunctionCallData> allFunctionCalls = functionCalls.Select(fc => fc.ToObject<RealtimeAiWssFunctionCallData>()).ToList();

                    parsedEvent.Data = allFunctionCalls;

                    foreach (var functionCallData in allFunctionCalls)
                    {
                        Log.Information("GoogleAdapter: Received tool call request: {FunctionName}", functionCallData.FunctionName);
                    }
                }
            }
            else if (jObject["toolCallCancellation"] is JObject toolCallCancellationObj)
            {
                parsedEvent.Type = RealtimeAiWssEventType.Unknown;
                JArray ids = toolCallCancellationObj["ids"] as JArray;
                parsedEvent.Data = new { CancelledToolCallIds = ids?.ToObject<List<string>>() };
                Log.Warning("GoogleAdapter: Received tool call cancellation request: Ids={CancelledToolCallIds}, Raw={RawJson}", ids?.ToString(), rawMessage);
            }
            else if (jObject["goAway"] is JObject goAwayObj)
            {
                parsedEvent.Type = RealtimeAiWssEventType.ConnectionStateChanged;
                parsedEvent.Data = new { TimeLeft = goAwayObj["timeLeft"]?.ToString() };
                Log.Warning("GoogleAdapter: Server sent GoAway notification: TimeLeft={TimeLeft}, Raw={RawJson}", goAwayObj["timeLeft"]?.ToString(), rawMessage);
            }
            else if (jObject["sessionResumptionUpdate"] is JObject sessionResumptionUpdateObj)
            {
                parsedEvent.Type = RealtimeAiWssEventType.SessionInitialized;
                parsedEvent.Data = sessionResumptionUpdateObj.ToObject<object>();
                Log.Information("GoogleAdapter: Received session resumption update, considering it as session initialized/updated: Resumable={Resumable}, NewHandle={NewHandle}, Raw={RawJson}",
                    sessionResumptionUpdateObj.Value<bool>("resumable"),
                    sessionResumptionUpdateObj.Value<string>("newHandle"),
                    rawMessage);
            }
            else
            {
                Log.Warning("GoogleAdapter: Unknown top-level Gemini server message structure: {RawJson}", rawMessage);
                parsedEvent.Type = RealtimeAiWssEventType.Unknown;
            }

            return parsedEvent;
        }
        catch (JsonException jsonEx)
        {
            Log.Error(jsonEx, "GoogleAdapter: Failed to parse JSON message: {RawMessage}", rawMessage);
            return new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.Error,
                RawJson = rawMessage,
                Data = new RealtimeAiErrorData
                    { Message = "JSON parsing failed: " + jsonEx.Message, IsCritical = true, Code = "JSON_PARSE_ERROR" }
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GoogleAdapter: An unexpected error occurred while parsing the message: {RawMessage}",
                rawMessage);
            return new ParsedRealtimeAiProviderEvent
            {
                Type = RealtimeAiWssEventType.Error,
                RawJson = rawMessage,
                Data = new RealtimeAiErrorData
                {
                    Message = "An unexpected error occurred while parsing the message: " + ex.Message,
                    IsCritical = true, Code = "UNEXPECTED_PARSE_ERROR"
                }
            };
        }
    }

    private void ParseServerContentIntoEvent(JObject serverContentJson, ParsedRealtimeAiProviderEvent eventToPopulate)
    {
        if (serverContentJson["generationComplete"]?.Value<bool>() == true)
        {
            if (eventToPopulate.Type == RealtimeAiWssEventType.ResponseTextDelta)
            {
                eventToPopulate.Type = RealtimeAiWssEventType.TranscriptionCompleted;
            }
            else if (eventToPopulate.Type == RealtimeAiWssEventType.ResponseAudioDelta)
            {
                eventToPopulate.Type = RealtimeAiWssEventType.ResponseAudioDone;
            }
            else if (eventToPopulate.Type == RealtimeAiWssEventType.FunctionCallSuggested)
            {
                eventToPopulate.Type = RealtimeAiWssEventType.ResponseTurnCompleted;
            }
            else if (eventToPopulate.Type == RealtimeAiWssEventType.Unknown && serverContentJson.HasValues)
            {
                eventToPopulate.Type = RealtimeAiWssEventType.ResponseTurnCompleted;
            }
        }
        else if (serverContentJson["interrupted"]?.Value<bool>() == true)
        {
            eventToPopulate.Type = RealtimeAiWssEventType.Error;
            eventToPopulate.Data = new RealtimeAiErrorData
                { Message = "模型生成被中断", IsCritical = false, Code = "GENERATION_INTERRUPTED" };
        }
        else if (serverContentJson["modelTurnContent"] is JObject modelTurnContent)
        {
            if (modelTurnContent["parts"] is JArray parts && parts.Count > 0)
            {
                StringBuilder accumulatedText = new StringBuilder();
                bool textPartFound = false;
                bool functionCallProcessed = false;

                foreach (var partToken in parts)
                {
                    if (partToken is JObject part)
                    {
                        if (part["functionCall"] is JObject functionCall)
                        {
                            eventToPopulate.Type = RealtimeAiWssEventType.FunctionCallSuggested;
                            eventToPopulate.Data = new RealtimeAiWssFunctionCallData
                            {
                                FunctionName = functionCall.Value<string>("name"),
                                ArgumentsJson = functionCall["args"]?.ToString(Formatting.None)
                            };
                            functionCallProcessed = true;
                            break;
                        }
                        else if (part["inlineData"] is JObject inlineData && inlineData["mimeType"] is JToken mimeTypeToken && inlineData["data"] is JToken dataToken)
                        {
                            string mimeType = mimeTypeToken.Value<string>();
                            string base64Data = dataToken.Value<string>();
                            if (mimeType != null && mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrEmpty(base64Data))
                            {
                                eventToPopulate.Type = RealtimeAiWssEventType.ResponseAudioDelta;
                                eventToPopulate.Data = new RealtimeAiWssAudioData { Base64Payload = base64Data };
                            }
                        }
                        else if (part["text"] is JToken textToken)
                        {
                            accumulatedText.Append(textToken.Value<string>());
                            textPartFound = true;
                            eventToPopulate.Type = RealtimeAiWssEventType.ResponseTextDelta;
                            eventToPopulate.Data = new RealtimeAiWssTranscriptionData
                                { Transcript = accumulatedText.ToString(), Speaker = AiSpeechAssistantSpeaker.Ai };
                        }
                    }
                }
            }
        }
        else if (serverContentJson["inputTranscription"] is JObject inputTranscriptionObj)
        {
            eventToPopulate.Type =
                RealtimeAiWssEventType.TranscriptionPartial; // 或者 TranscriptionCompleted，取决于 API 是否分段发送
            eventToPopulate.Data = inputTranscriptionObj.ToObject<RealtimeAiWssTranscriptionData>();
            if (eventToPopulate.Data is RealtimeAiWssTranscriptionData transcriptionData)
            {
                transcriptionData.Speaker = AiSpeechAssistantSpeaker.User; // 假设 inputTranscription 是用户说的
            }
        }
        else if (serverContentJson["outputTranscription"] is JObject outputTranscriptionObj)
        {
            eventToPopulate.Type = RealtimeAiWssEventType.ResponseTextDelta; // 或者其他更合适的类型
            eventToPopulate.Data = outputTranscriptionObj.ToObject<RealtimeAiWssTranscriptionData>();
            if (eventToPopulate.Data is RealtimeAiWssTranscriptionData transcriptionData)
            {
                transcriptionData.Speaker = AiSpeechAssistantSpeaker.Ai; // 假设 outputTranscription 是 AI 说的
            }
        }
        else if (serverContentJson.HasValues)
        {
            Log.Warning("GoogleAdapter: Unhandled fields in serverContent: {ServerContentJson}",
                serverContentJson.ToString(Formatting.None));
        }
    }

    private bool IsTerminalFinishReason(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return false;
        return reason == "STOP" || reason == "MAX_TOKENS" || reason == "SAFETY" || reason == "RECITATION";
    }

    // Ensure this method correctly uses Newtonsoft.Json (as in the previous version)
    private async Task<List<(AiSpeechAssistantSessionConfigType Type, object Config)>> InitialSessionConfigAsync(Domain.AISpeechAssistant.AiSpeechAssistant assistant, CancellationToken cancellationToken = default)
    {
        var functions = await _aiSpeechAssistantDataProvider.GetAiSpeechAssistantFunctionCallByAssistantIdAsync(assistant.Id, cancellationToken).ConfigureAwait(false);

        if (functions == null || functions.Count == 0) return new List<(AiSpeechAssistantSessionConfigType Type, object Config)>();

        return functions
            .Where(x => !string.IsNullOrWhiteSpace(x.Content))
            .Select(x =>
            {
                object deserializedConfig = null;
                try
                {
                    // Ensure using Newtonsoft.Json
                    deserializedConfig = JsonConvert.DeserializeObject<object>(x.Content);
                }
                catch (Newtonsoft.Json.JsonException ex) // Explicitly Newtonsoft.Json.JsonException
                {
                    Log.Error(ex, "GoogleAdapter InitialSessionConfigAsync: Failed to deserialize function configuration using Newtonsoft.Json for Type {ConfigType}, Content: {ConfigContent}", x.Type, x.Content);
                }
                return (x.Type, deserializedConfig);
            })
            .Where(x => x.deserializedConfig != null)
            .ToList();
    }
}