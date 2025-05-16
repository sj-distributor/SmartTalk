using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Dto.RealtimeAi;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using JsonException = Newtonsoft.Json.JsonException;

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
            using var jsonDocument = JsonDocument.Parse(rawMessage);
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("setupComplete", out _))
            {
                parsedEvent.Type = RealtimeAiWssEventType.SessionInitialized;
                Log.Information("GoogleAdapter: Received setupComplete message.");
            }
            else if (root.TryGetProperty("serverContent", out var serverContentProp))
            {
                // 将 JsonElement 传递给新的 ParseServerContent 方法
                ParseServerContentIntoEvent(serverContentProp);
            }
            else if (root.TryGetProperty("toolCall", out var toolCallProp))
            {
                parsedEvent.Type = RealtimeAiWssEventType.FunctionCallSuggested;
                if (toolCallProp.TryGetProperty("functionCalls", out var functionCallsArray) &&
                    functionCallsArray.ValueKind == JsonValueKind.Array)
                {
                    var allFunctionCalls = new List<RealtimeAiWssFunctionCallData>();
                    foreach (var functionCallElement in functionCallsArray.EnumerateArray())
                    {
                        try
                        {
                            var functionName = functionCallElement.TryGetProperty("name", out var nameProp)
                                ? nameProp.GetString()
                                : null;
                            var argumentsJson = functionCallElement.TryGetProperty("args", out var argsProp)
                                ? argsProp.GetRawText()
                                : null;
                            allFunctionCalls.Add(new RealtimeAiWssFunctionCallData
                                { FunctionName = functionName, ArgumentsJson = argumentsJson });
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "GoogleAdapter: Failed to parse function call: {Json}",
                                functionCallElement.GetRawText());
                        }
                    }

                    parsedEvent.Data = allFunctionCalls;
                    if (allFunctionCalls.Count > 0)
                    {
                        Log.Information("GoogleAdapter: Received tool call request(s). Count: {Count}",
                            allFunctionCalls.Count);
                    }
                }
            }
            else if (root.TryGetProperty("goAway", out var goAwayProp))
            {
                parsedEvent.Type = RealtimeAiWssEventType.ConnectionStateChanged;
                var timeLeft = goAwayProp.TryGetProperty("timeLeft", out var timeLeftProp)
                    ? timeLeftProp.GetString()
                    : null;
                parsedEvent.Data = new { Reason = "Server is going away", TimeLeft = timeLeft };
                Log.Warning("GoogleAdapter: Server sent GoAway notification. TimeLeft={TimeLeft}", timeLeft);
            }
            else if (root.TryGetProperty("sessionResumptionUpdate", out var sessionResumptionUpdateProp))
            {
                parsedEvent.Type = RealtimeAiWssEventType.SessionInitialized;
                parsedEvent.Data = JsonDocument.Parse(sessionResumptionUpdateProp.GetRawText()).RootElement;
                Log.Information(
                    "GoogleAdapter: Received session resumption update, considering it as session initialized/updated: {Json}",
                    sessionResumptionUpdateProp.GetRawText());
            }
            else if (root.TryGetProperty("usageMetadata", out var usageMetadataProp))
            {
                parsedEvent.Type = RealtimeAiWssEventType.Unknown;
                parsedEvent.Data = JsonDocument.Parse(usageMetadataProp.GetRawText()).RootElement;
                Log.Information("GoogleAdapter: Received Usage Metadata: {UsageMetadataJson}",
                    usageMetadataProp.GetRawText());
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
                Data = new RealtimeAiErrorData { Message = "JSON parsing failed: " + jsonEx.Message, IsCritical = true }
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
                    Message = "An unexpected error occurred while parsing the message: " + ex.Message, IsCritical = true
                }
            };
        }
    }

    private ParsedRealtimeAiProviderEvent ParseServerContentIntoEvent(JsonElement serverContentJson)
    {
        var parsedEvent = new ParsedRealtimeAiProviderEvent { RawJson = serverContentJson.GetRawText() };

        if (serverContentJson.TryGetProperty("generationComplete", out var generationCompleteProp) &&
            generationCompleteProp.GetBoolean())
        {
            parsedEvent.Type = RealtimeAiWssEventType.ResponseTurnCompleted; // 模型生成完毕，视为一个 Turn 完成
        }
        else if (serverContentJson.TryGetProperty("turnComplete", out var turnCompleteProp) &&
                 turnCompleteProp.GetBoolean())
        {
            parsedEvent.Type = RealtimeAiWssEventType.ResponseTurnCompleted; // 模型完成其回合
        }
        else if (serverContentJson.TryGetProperty("interrupted", out var interruptedProp) &&
                 interruptedProp.GetBoolean())
        {
            parsedEvent.Type = RealtimeAiWssEventType.Error;
            parsedEvent.Data = new RealtimeAiErrorData
            {
                Message = "模型生成被中断",
                IsCritical = false,
                Code = "GENERATION_INTERRUPTED"
            };
        }
        else if (serverContentJson.TryGetProperty("inputTranscription", out var inputTranscriptionProp) &&
                 inputTranscriptionProp.ValueKind == JsonValueKind.Object)
        {
            parsedEvent.Type = RealtimeAiWssEventType.TranscriptionPartial;
            try
            {
                var text = inputTranscriptionProp.TryGetProperty("text", out var textProp)
                    ? textProp.GetString()
                    : null;
                parsedEvent.Data = new RealtimeAiWssTranscriptionData
                    { Transcript = text, Speaker = AiSpeechAssistantSpeaker.User };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GoogleAdapter: Failed to parse input transcription: {Json}",
                    inputTranscriptionProp.GetRawText());
            }
        }
        else if (serverContentJson.TryGetProperty("outputTranscription", out var outputTranscriptionProp) &&
                 outputTranscriptionProp.ValueKind == JsonValueKind.Object)
        {
            parsedEvent.Type = RealtimeAiWssEventType.ResponseTextDelta;
            try
            {
                var text = outputTranscriptionProp.TryGetProperty("text", out var textProp)
                    ? textProp.GetString()
                    : null;
                parsedEvent.Data = new RealtimeAiWssTranscriptionData
                    { Transcript = text, Speaker = AiSpeechAssistantSpeaker.Ai };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GoogleAdapter: Failed to parse output transcription: {Json}",
                    outputTranscriptionProp.GetRawText());
            }
        }
        else if (serverContentJson.TryGetProperty("modelTurn", out var modelTurnProp) && modelTurnProp.ValueKind == JsonValueKind.Object)
        {
            ParseModelTurnParts(modelTurnProp, parsedEvent);
        }
        else if (serverContentJson.ValueKind == JsonValueKind.Object && serverContentJson.EnumerateObject().Any())
        {
            Log.Warning("GoogleAdapter: Unhandled fields in serverContent: {ServerContentJson}",
                serverContentJson.GetRawText());
            parsedEvent.Type = RealtimeAiWssEventType.Unknown;
        }
        else
        {
            parsedEvent.Type = RealtimeAiWssEventType.Unknown;
        }

        return parsedEvent;
    }

    private void ParseModelTurnParts(JsonElement partsArray, ParsedRealtimeAiProviderEvent eventToPopulate)
{
    StringBuilder accumulatedText = new StringBuilder();
    bool functionCallProcessed = false;
    bool audioPartFound = false;

    foreach (var partElement in partsArray.EnumerateArray())
    {
        if (partElement.ValueKind == JsonValueKind.Object)
        {
            string partType = null;
            if (partElement.TryGetProperty("functionCall", out _)) partType = "functionCall";
            else if (partElement.TryGetProperty("inlineData", out var inlineDataProp) &&
                     inlineDataProp.ValueKind == JsonValueKind.Object &&
                     inlineDataProp.TryGetProperty("mimeType", out var mimeTypeProp) &&
                     mimeTypeProp.GetString()?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true &&
                     inlineDataProp.TryGetProperty("data", out _)) partType = "audio";
            else if (partElement.TryGetProperty("text", out _)) partType = "text";
            else if (partElement.TryGetProperty("codeExecutionResult", out _)) partType = "codeExecutionResult";

            switch (partType)
            {
                case "functionCall":
                    eventToPopulate.Type = RealtimeAiWssEventType.FunctionCallSuggested;
                    try
                    {
                        var functionCallProp = partElement.GetProperty("functionCall");
                        var functionName = functionCallProp.TryGetProperty("name", out var nameProp)
                            ? nameProp.GetString()
                            : null;
                        var argumentsJson = functionCallProp.TryGetProperty("args", out var argsProp)
                            ? argsProp.GetRawText()
                            : null;
                        eventToPopulate.Data = new RealtimeAiWssFunctionCallData
                            { FunctionName = functionName, ArgumentsJson = argumentsJson };
                        functionCallProcessed = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "GoogleAdapter: Failed to parse function call in modelTurn: {Json}",
                            partElement.GetProperty("functionCall").GetRawText());
                    }
                    break;
                case "audio":
                    eventToPopulate.Type = RealtimeAiWssEventType.ResponseAudioDelta;
                    try
                    {
                        var inlineDataProp = partElement.GetProperty("inlineData");
                        var dataProp = inlineDataProp.GetProperty("data").GetString();
                        eventToPopulate.Data = new RealtimeAiWssAudioData { Base64Payload = dataProp };
                        audioPartFound = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "GoogleAdapter: Failed to parse audio data in modelTurn: {Json}",
                            partElement.GetProperty("inlineData").GetRawText());
                    }
                    break;
                case "text":
                    if (!functionCallProcessed)
                    {
                        var textProp = partElement.GetProperty("text").GetString();
                        accumulatedText.Append(textProp);
                        eventToPopulate.Type = RealtimeAiWssEventType.ResponseTextDelta;
                        eventToPopulate.Data = new RealtimeAiWssTranscriptionData
                            { Transcript = accumulatedText.ToString(), Speaker = AiSpeechAssistantSpeaker.Ai };
                    }
                    break;
                case "codeExecutionResult":
                    try
                    {
                        var codeExecutionResultProp = partElement.GetProperty("codeExecutionResult");
                        string outcome = codeExecutionResultProp.TryGetProperty("outcome", out var outcomeProp) ? outcomeProp.GetString() : null;
                        string output = codeExecutionResultProp.TryGetProperty("output", out var outputProp) ? outputProp.GetString() : null;

                        if (outcome == "OUTCOME_FAILED" || outcome == "OUTCOME_DEADLINE_EXCEEDED")
                        {
                            eventToPopulate.Type = RealtimeAiWssEventType.Error;
                            eventToPopulate.Data = new RealtimeAiErrorData { Message = $"代码执行 {outcome}: {output}", IsCritical = outcome == "OUTCOME_DEADLINE_EXCEEDED", Code = outcome };
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "GoogleAdapter: Failed to parse code execution result in modelTurn: {Json}",
                            partElement.GetProperty("codeExecutionResult").GetRawText());
                    }
                    break;
            }
        }
    }

    if (!functionCallProcessed && accumulatedText.Length > 0 && eventToPopulate.Type == RealtimeAiWssEventType.ResponseTextDelta)
    {
        // 确保最终设置了文本数据 (如果之前有文本 part)
        eventToPopulate.Data = new RealtimeAiWssTranscriptionData
            { Transcript = accumulatedText.ToString(), Speaker = AiSpeechAssistantSpeaker.Ai };
    }
    else if (!functionCallProcessed && audioPartFound && eventToPopulate.Type == RealtimeAiWssEventType.Unknown)
    {
        eventToPopulate.Type = RealtimeAiWssEventType.ResponseAudioDelta;
    }
}
    
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
                    deserializedConfig = JsonConvert.DeserializeObject<object>(x.Content);
                }
                catch (JsonException ex)
                {
                    Log.Error(ex, "GoogleAdapter InitialSessionConfigAsync: Failed to deserialize function configuration using Newtonsoft.Json for Type {ConfigType}, Content: {ConfigContent}", x.Type, x.Content);
                }
                return (x.Type, deserializedConfig);
            })
            .Where(x => x.deserializedConfig != null)
            .ToList();
    }
}