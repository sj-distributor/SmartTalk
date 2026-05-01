using Serilog;
using Lingua;
using Newtonsoft.Json.Linq;
using OpenAI.Audio;
using OpenAI.Chat;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Enums.STT;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken);

    Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken);

    Task<DetectAudioLanguageResponse> DetectAudioLanguageAsync(DetectAudioLanguageCommand command, CancellationToken cancellationToken);

    Task<TranscribeAndDetectAudioLanguageResponse> TranscribeAndDetectAudioLanguageAsync(TranscribeAndDetectAudioLanguageCommand command, CancellationToken cancellationToken);

    Task<TranscribeDiarizedAudioResponse> TranscribeDiarizedAudioAsync(TranscribeDiarizedAudioCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
    private static readonly Regex TranscriptTokenRegex = new(
        @"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]+|[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7AF]+|[\p{L}]+(?:['’-][\p{L}]+)*",
        RegexOptions.Compiled);

    public async Task<DetectAudioLanguageResponse> DetectAudioLanguageAsync(DetectAudioLanguageCommand command, CancellationToken cancellationToken)
    {
        var audioContent = await _httpClientFactory
            .GetAsync<byte[]>(command.RecordingUrl, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken).ConfigureAwait(false);

        var languageCode = await DetectAudioLanguageOnceAsync(audioContent, cancellationToken).ConfigureAwait(false);

        Log.Information("Detect audio language by url. RecordingUrl: {RecordingUrl}, Result: {Result}", command.RecordingUrl, languageCode);

        return new DetectAudioLanguageResponse
        {
            Data = languageCode
        };
    }

    public async Task<TranscribeAndDetectAudioLanguageResponse> TranscribeAndDetectAudioLanguageAsync(TranscribeAndDetectAudioLanguageCommand command, CancellationToken cancellationToken)
    {
        var audioContent = await _httpClientFactory
            .GetAsync<byte[]>(command.RecordingUrl, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken).ConfigureAwait(false);

        var transcript = await TranscribeAudioAsync(audioContent, cancellationToken).ConfigureAwait(false);
        var detection = DetectDominantLanguageFromTranscript(transcript);

        Log.Information(
            "Transcribe and detect audio language by url. RecordingUrl: {RecordingUrl}, Result: {Result}, TranscriptLength: {TranscriptLength}",
            command.RecordingUrl,
            detection.Language,
            transcript?.Length ?? 0);

        return new TranscribeAndDetectAudioLanguageResponse
        {
            Data = detection
        };
    }

    public async Task<TranscribeDiarizedAudioResponse> TranscribeDiarizedAudioAsync(TranscribeDiarizedAudioCommand command, CancellationToken cancellationToken)
    {
        var audioContent = await _httpClientFactory
            .GetAsync<byte[]>(command.RecordingUrl, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken).ConfigureAwait(false);

        var extension = ResolveAudioFileExtension(command.RecordingUrl);
        var diarizedSegments = await TranscribeDiarizedAudioSegmentsAsync(audioContent, extension, cancellationToken).ConfigureAwait(false);

        Log.Information(
            "Transcribe diarized audio by url. RecordingUrl: {RecordingUrl}, SegmentCount: {SegmentCount}",
            command.RecordingUrl,
            diarizedSegments.Count);

        return new TranscribeDiarizedAudioResponse
        {
            Data = diarizedSegments
        };
    }

    public async Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken)
    {
        await RetryHelper.RetryAsync(async () =>
        {
            await _twilioService.CreateRecordingAsync(
                command.CallSid,
                new Uri($"https://{command.Host}/api/AiSpeechAssistant/recording/callback"));
        }, maxRetryCount: 5, delaySeconds: 5, cancellationToken);
    }

    public async Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken)
    {
        Log.Information("Handling ReceivePhoneRecord command: {@Command}", command);

        var (record, agent, assistant) = await RetryHelper.RetryOnResultAsync(
            ct => _phoneOrderDataProvider.GetRecordWithAgentAndAssistantAsync(command.CallSid, ct),
            result => result.Item1 == null,
            maxRetryCount: 3,
            delay: TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);

        Log.Information("Handling ReceivePhoneRecord phone order record: {@Record}", record);

        record.Url = command.RecordingUrl;

        var audioFileRawBytes = await _httpClientFactory
            .GetAsync<byte[]>(record.Url, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken).ConfigureAwait(false);

        if (agent is { IsSendAudioRecordWechat: true })
        {
            var recordingUrl = record.Url;
            
            if (record.Url.Contains("twilio"))
            {
                var uploadedAudio = await _attachmentService
                    .UploadAttachmentAsync(new UploadAttachmentCommand { Attachment = new UploadAttachmentDto { FileName = Guid.NewGuid() + ".wav", FileContent = audioFileRawBytes } }, cancellationToken).ConfigureAwait(false);

                Log.Information("Handling ReceivePhoneRecord audio uploaded, url: {Url}", uploadedAudio?.Attachment?.FileUrl);

                if (string.IsNullOrEmpty(uploadedAudio?.Attachment?.FileUrl) || agent.Id == 0) return;

                recordingUrl = uploadedAudio.Attachment?.FileUrl;
            }

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, agent.WechatRobotKey, $"来电电话：{record.IncomingCallNumber ?? ""}\n\n您有一条新的AI通话录音：\n{recordingUrl}", Array.Empty<string>(), cancellationToken).ConfigureAwait(false);
        }

        var language = string.Empty;
        try
        {
            var defaultLanguageCode = ResolveDefaultLanguageCode(assistant?.ModelLanguage);
            language = await DetectAudioLanguageAsync(audioFileRawBytes, defaultLanguageCode, cancellationToken).ConfigureAwait(false);

            await SendServerRestoreMessageIfNecessaryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            const string alertMessage = "服务器异常。";

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, alertMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
            await _cacheManager.GetOrAddAsync("gpt-4o-audio-exception", _ => Task.FromResult(Task.FromResult(alertMessage)), new RedisCachingSetting(RedisServer.System, TimeSpan.FromDays(1)), cancellationToken).ConfigureAwait(false);
        }

        record.Language = ConvertLanguageCode(language);
        record.TranscriptionJobId = await _speechMaticsService.CreateSpeechMaticsJobAsync(audioFileRawBytes, Guid.NewGuid().ToString("N") + ".wav", language, SpeechMaticsJobScenario.Released, cancellationToken).ConfigureAwait(false);

        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private TranscriptionLanguage ConvertLanguageCode(string languageCode)
    {
        return languageCode switch
        {
            "en" => TranscriptionLanguage.English,
            "es" => TranscriptionLanguage.Spanish,
            "ko" => TranscriptionLanguage.Korean,
            _ => TranscriptionLanguage.Chinese
        };
    }

    private async Task<string> DetectAudioLanguageAsync(byte[] audioContent, string defaultLanguageCode, CancellationToken cancellationToken)
    {
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        var audioData = BinaryData.FromBytes(audioContent);
        var normalizedDefaultLanguageCode = NormalizeLanguageCode(defaultLanguageCode) ?? "en";

        List<ChatMessage> messages =
        [
            new SystemChatMessage("""
                                  You are a professional speech recognition analyst. Based on the audio content, determine the main language used and return only one language code from the following options:
                                  zh-CN: Mandarin (Simplified Chinese)
                                  zh: Cantonese
                                  zh-TW: Taiwanese Chinese (Traditional Chinese)
                                  en: English
                                  es: Spanish
                                  ko: Korean

                                  Rules:
                                  1. Carefully analyze the entire speech content and identify the **dominant spoken language**, not just occasional words or short phrases.
                                  2. If the recording contains noise, background sounds, or non-standard pronunciation, focus on consistent linguistic features such as tone, rhythm, and pronunciation pattern.
                                  3. **Do NOT confuse accented English with Chinese.** English spoken with a Chinese accent or non-standard pronunciation must still be classified as English (en).
                                  4. Only return 'es' (Spanish) if the majority of the recording is clearly and consistently spoken in Spanish. Do NOT classify English with Spanish-like sounds or background as Spanish.
                                  5. If the recording mixes languages, return the code of the language that dominates the majority of the speaking time.
                                  6. **If you are uncertain between English and Chinese, always choose English (en).**
                                  7. Return only the code without any additional text, punctuation, or explanations.

                                  Examples:
                                  If the audio is in Mandarin, even with background noise, return: zh-CN
                                  If the audio is in Cantonese, possibly with some Mandarin words, return: zh
                                  If the audio is in Taiwanese Mandarin (Traditional Chinese), return: zh-TW
                                  If the audio is in English, even with a strong accent or imperfect pronunciation, return: en
                                  If the audio is in English with background noise, return: en
                                  If the audio is predominantly in Spanish, spoken clearly and throughout most of the recording, return: es
                                  If the audio is predominantly in Korean, spoken clearly and throughout most of the recording, return: ko
                                  If the audio has both Mandarin and English but Mandarin is the dominant language, return: zh-CN
                                  If the audio has both Cantonese and English but English dominates, return: en
                                  If the audio is in English but contains occasional Chinese filler words such as "啊", "嗯", or "對", return: en
                                  If the audio is mainly in Chinese but the speaker occasionally uses short English words like "OK", "yeah", or "sorry", return: zh-CN
                                  If the recording has Chinese background speech but the main speaker talks in English, return: en
                                  If the recording has multiple speakers where one speaks English and others speak Mandarin, determine which language dominates most of the speaking time and return that language code.
                                  If the audio is short and contains only a few clear English words, classify as English (en).
                                  If the audio is mostly silent, unclear, or contains indistinguishable sounds, choose the language that can be most confidently recognized based on speech features, not noise.

                                  """),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("Please determine the language based on the recording and return the corresponding code.")
        ];

        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion firstCompletion = await client.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var firstPassLanguageCode = NormalizeLanguageCode(firstCompletion.Content.FirstOrDefault()?.Text) ?? normalizedDefaultLanguageCode;

        Log.Information("Detect audio language first pass. Default: {DefaultLanguage}, Result: {Result}",
            normalizedDefaultLanguageCode, firstPassLanguageCode);

        if (string.Equals(firstPassLanguageCode, normalizedDefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return firstPassLanguageCode;
        }

        List<ChatMessage> compareMessages =
        [
            new SystemChatMessage($$"""
                                    You are a professional speech recognition analyst.
                                    Re-check the audio and return only one language code from:
                                    zh-CN, zh, zh-TW, en, es, ko

                                    First-pass detected language: {{firstPassLanguageCode}}
                                    Call default language: {{normalizedDefaultLanguageCode}}

                                    Rules:
                                    1. Analyze the dominant spoken language across the whole audio.
                                    2. If first-pass result and default language conflict, compare them carefully with the audio again.
                                    3. If evidence is not strong enough to overturn the default language, keep the default language.
                                    4. Return only one code without extra text.
                                    """),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("Re-check this recording and return the final language code only.")
        ];

        ChatCompletion secondCompletion = await client.CompleteChatAsync(compareMessages, options, cancellationToken).ConfigureAwait(false);
        var secondPassLanguageCode = NormalizeLanguageCode(secondCompletion.Content.FirstOrDefault()?.Text);
        var finalLanguageCode = secondPassLanguageCode ?? normalizedDefaultLanguageCode;

        Log.Information(
            "Detect audio language second pass. Default: {DefaultLanguage}, FirstPass: {FirstPass}, SecondPass: {SecondPass}, Final: {Final}",
            normalizedDefaultLanguageCode,
            firstPassLanguageCode,
            secondPassLanguageCode ?? "<invalid>",
            finalLanguageCode);

        return finalLanguageCode;
    }

    private async Task<string> DetectAudioLanguageOnceAsync(byte[] audioContent, CancellationToken cancellationToken)
    {
        ChatClient client = new("gpt-4o-audio-preview", _openAiSettings.ApiKey);

        var audioData = BinaryData.FromBytes(audioContent);
        List<ChatMessage> messages =
        [
            new SystemChatMessage("""
                                  You are a professional speech recognition analyst. Based on the audio content, determine the main language used and return only one language code from the following options:
                                  zh-CN: Mandarin (Simplified Chinese)
                                  zh: Cantonese
                                  zh-TW: Taiwanese Chinese (Traditional Chinese)
                                  en: English
                                  es: Spanish
                                  ko: Korean

                                  Rules:
                                  1. Carefully analyze the entire speech content and identify the dominant spoken language, not just occasional words or short phrases.
                                  2. If the recording contains noise, background sounds, or non-standard pronunciation, focus on consistent linguistic features such as tone, rhythm, and pronunciation pattern.
                                  3. Do NOT confuse accented English with Chinese. English spoken with a Chinese accent or non-standard pronunciation must still be classified as English (en).
                                  4. Only return 'es' (Spanish) if the majority of the recording is clearly and consistently spoken in Spanish. Do NOT classify English with Spanish-like sounds or background as Spanish.
                                  5. If the recording mixes languages, return the code of the language that dominates the majority of the speaking time.
                                  6. If you are uncertain between English and Chinese, always choose English (en).
                                  7. Return only the code without any additional text, punctuation, or explanations.
                                  """),
            new UserChatMessage(ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav)),
            new UserChatMessage("Please determine the language based on the recording and return the corresponding code.")
        ];

        ChatCompletionOptions options = new() { ResponseModalities = ChatResponseModalities.Text };

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var languageCode = NormalizeLanguageCode(completion.Content.FirstOrDefault()?.Text) ?? "en";

        Log.Information("Detect audio language single pass result: {Result}", languageCode);

        return languageCode;
    }

    private async Task<string> TranscribeAudioAsync(byte[] audioContent, CancellationToken cancellationToken)
    {
        AudioClient client = new("gpt-4o-transcribe", _openAiSettings.ApiKey);

        await using var audioStream = new MemoryStream(audioContent);

        AudioTranscriptionOptions options = new()
        {
            ResponseFormat = AudioTranscriptionFormat.Text
        };

        var transcription = await client
            .TranscribeAudioAsync(audioStream, "recording.wav", options, cancellationToken)
            .ConfigureAwait(false);

        return transcription.Value?.Text?.Trim() ?? string.Empty;
    }

    private async Task<List<TranscribeDiarizedAudioSegmentDto>> TranscribeDiarizedAudioSegmentsAsync(
        byte[] audioContent,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        var requestUrl = $"{ResolveOpenAiBaseUrl()}/v1/audio/transcriptions";
        var requestFileName = $"recording{fileExtension}";
        var requestBaseUrl = ResolveOpenAiBaseUrl();

        Log.Information(
            "Starting OpenAI diarized transcription. Url: {Url}, BaseUrl: {BaseUrl}, FileName: {FileName}, FileSize: {FileSize}",
            requestUrl,
            requestBaseUrl,
            requestFileName,
            audioContent?.Length ?? 0);

        if ((audioContent?.Length ?? 0) > 25 * 1024 * 1024)
        {
            Log.Warning(
                "OpenAI diarized transcription input exceeds documented file size limit. FileName: {FileName}, FileSize: {FileSize}",
                requestFileName,
                audioContent?.Length ?? 0);
        }

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_openAiSettings.ApiKey}" }
        };

        var responseText = await _httpClientFactory.PostAsMultipartAsync<string>(
            requestUrl,
            new Dictionary<string, string>
            {
                ["model"] = "gpt-4o-transcribe-diarize",
                ["response_format"] = "diarized_json",
                ["chunking_strategy"] = "auto"
            },
            new Dictionary<string, (byte[], string)>
            {
                ["file"] = (audioContent, requestFileName)
            },
            cancellationToken,
            timeout: TimeSpan.FromMinutes(10),
            headers: headers,
            isNeedToReadErrorContent: true).ConfigureAwait(false);

        Log.Information(
            "OpenAI diarized transcription raw response received. BodyPreview: {BodyPreview}",
            BuildResponsePreview(responseText));

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("OpenAI diarized transcription returned an empty response body.");
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(responseText);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse OpenAI diarized transcription response. BodyPreview: {BodyPreview}", BuildResponsePreview(responseText));
            throw new InvalidOperationException(
                $"OpenAI diarized transcription returned non-JSON content. Body={BuildResponsePreview(responseText)}",
                ex);
        }

        if (payload["error"] is JObject error)
        {
            Log.Error("OpenAI diarized transcription returned an error payload: {Error}", error.ToString());
            var statusCode = payload.Value<int?>("status");
            throw new InvalidOperationException(
                $"OpenAI diarized transcription request failed. StatusCode={statusCode?.ToString() ?? "unknown"}, Body={BuildResponsePreview(responseText)}");
        }

        var segments = payload["segments"] as JArray;
        var text = payload.Value<string>("text")?.Trim() ?? string.Empty;

        Log.Information(
            "Parsed OpenAI diarized transcription payload. Keys: {Keys}, SegmentCount: {SegmentCount}, TranscriptLength: {TranscriptLength}",
            string.Join(", ", payload.Properties().Select(p => p.Name)),
            segments?.Count ?? 0,
            text.Length);

        if (segments == null || segments.Count == 0)
        {
            throw new InvalidOperationException(
                $"OpenAI diarized transcription did not return any segments. Body={BuildResponsePreview(responseText)}");
        }

        var speakerRoles = ResolveSpeakerRoles(segments);

        return segments
            .OfType<JObject>()
            .Select(segment => new TranscribeDiarizedAudioSegmentDto
            {
                Start = segment.Value<double?>("start") ?? 0,
                End = segment.Value<double?>("end") ?? 0,
                Speaker = segment.Value<string>("speaker"),
                Role = ResolveSpeakerRole(segment.Value<string>("speaker"), speakerRoles),
                Text = segment.Value<string>("text")?.Trim()
            })
            .Where(segment => !string.IsNullOrWhiteSpace(segment.Text))
            .ToList();
    }

    private TranscribeAndDetectAudioLanguageResponseData DetectDominantLanguageFromTranscript(string transcript)
    {
        var detector = LanguageDetectorBuilder
            .FromAllLanguages()
            .Build();

        var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalWeight = 0;

        foreach (Match match in TranscriptTokenRegex.Matches(transcript ?? string.Empty))
        {
            var token = match.Value.Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var languageCode = MapLinguaLanguageToCode(detector.DetectLanguageOf(token));

            if (string.IsNullOrWhiteSpace(languageCode))
            {
                continue;
            }

            var weight = token.Length;
            totalWeight += weight;

            if (!weights.TryAdd(languageCode, weight))
            {
                weights[languageCode] += weight;
            }
        }

        if (totalWeight == 0 && !string.IsNullOrWhiteSpace(transcript))
        {
            var fallbackLanguageCode = MapLinguaLanguageToCode(detector.DetectLanguageOf(transcript)) ?? "unknown";
            weights[fallbackLanguageCode] = 1;
            totalWeight = 1;
        }

        var ratios = weights
            .OrderByDescending(x => x.Value)
            .Select(x => new TranscribeAndDetectAudioLanguageRatioDto
            {
                Language = x.Key,
                Weight = x.Value,
                Ratio = Math.Round((double)x.Value / totalWeight, 4)
            })
            .ToList();

        return new TranscribeAndDetectAudioLanguageResponseData
        {
            Language = ratios.FirstOrDefault()?.Language ?? "unknown",
            Transcript = transcript,
            Ratios = ratios
        };
    }

    private string ResolveDefaultLanguageCode(string assistantModelLanguage)
    {
        return NormalizeLanguageCode(assistantModelLanguage) ?? "en";
    }

    private string ResolveOpenAiBaseUrl()
    {
        return string.IsNullOrWhiteSpace(_openAiSettings.BaseUrl)
            ? "https://api.openai.com"
            : _openAiSettings.BaseUrl.TrimEnd('/');
    }

    private string ResolveAudioFileExtension(string recordingUrl)
    {
        if (!Uri.TryCreate(recordingUrl, UriKind.Absolute, out var uri))
        {
            return ".wav";
        }

        var extension = Path.GetExtension(uri.AbsolutePath);

        return string.IsNullOrWhiteSpace(extension) ? ".wav" : extension;
    }

    private string BuildResponsePreview(string responseText, int maxLength = 1200)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return "<empty>";
        }

        var normalized = responseText.Replace("\r", " ").Replace("\n", " ").Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

    private Dictionary<string, string> ResolveSpeakerRoles(JArray segments)
    {
        var speakerRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var speaker in segments
                     .OfType<JObject>()
                     .Select(segment => segment.Value<string>("speaker"))
                     .Where(speaker => !string.IsNullOrWhiteSpace(speaker))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var explicitRole = TryResolveExplicitRole(speaker);

            if (!string.IsNullOrWhiteSpace(explicitRole))
            {
                speakerRoles[speaker] = explicitRole;
                continue;
            }

            speakerRoles[speaker] = speakerRoles.Count switch
            {
                0 => "客服",
                1 => "用户",
                _ => "未知"
            };
        }

        return speakerRoles;
    }

    private string ResolveSpeakerRole(string speaker, Dictionary<string, string> speakerRoles)
    {
        if (string.IsNullOrWhiteSpace(speaker))
        {
            return "未知";
        }

        return speakerRoles.TryGetValue(speaker, out var role) ? role : "未知";
    }

    private string TryResolveExplicitRole(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
        {
            return null;
        }

        var normalizedSpeaker = speaker.Trim().ToLowerInvariant();

        if (normalizedSpeaker.Contains("agent") ||
            normalizedSpeaker.Contains("assistant") ||
            normalizedSpeaker.Contains("support") ||
            normalizedSpeaker.Contains("客服") ||
            normalizedSpeaker.Contains("店员") ||
            normalizedSpeaker.Contains("商家"))
        {
            return "客服";
        }

        if (normalizedSpeaker.Contains("customer") ||
            normalizedSpeaker.Contains("client") ||
            normalizedSpeaker.Contains("caller") ||
            normalizedSpeaker.Contains("user") ||
            normalizedSpeaker.Contains("用户") ||
            normalizedSpeaker.Contains("顾客"))
        {
            return "用户";
        }

        return null;
    }

    private string MapLinguaLanguageToCode(Language language)
    {
        if (language == Language.Unknown)
        {
            return null;
        }

        var isoCode = LanguageInfo.IsoCode6391(language);

        if (isoCode == IsoCode6391.None)
        {
            return null;
        }

        return isoCode.ToString().ToLowerInvariant();
    }

    private string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var normalized = languageCode.Trim()
            .Trim('"', '\'', '`', '.', ',', ';', ':', '!', '?')
            .ToLowerInvariant();

        return normalized switch
        {
            "en" or "english" or "en-us" or "en-gb" => "en",
            "es" or "spanish" or "es-es" or "es-mx" or "espanol" => "es",
            "ko" or "korean" or "ko-kr" => "ko",
            "zh-cn" or "zh_cn" or "zh-hans" or "mandarin" or "chinese" or "simplified chinese" => "zh-CN",
            "zh" => "zh",
            "zh-tw" or "zh_tw" or "zh-hant" or "traditional chinese" or "taiwanese chinese" => "zh-TW",
            "cantonese" or "yue" or "zh-hk" or "zh_hk" => "zh",
            _ when normalized.StartsWith("en-", StringComparison.Ordinal) => "en",
            _ when normalized.StartsWith("es-", StringComparison.Ordinal) => "es",
            _ when normalized.StartsWith("ko-", StringComparison.Ordinal) => "ko",
            _ => null
        };
    }

    private async Task SendServerRestoreMessageIfNecessaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exceptionAlert = await _cacheManager.GetAsync<string>("gpt-4o-audio-exception", new RedisCachingSetting(), cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(exceptionAlert))
            {
                const string restoreMessage = "服务器恢复。";

                await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, restoreMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);

                await _cacheManager.RemoveAsync("gpt-4o-audio-exception", new RedisCachingSetting(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            // ignored
        }
    }
}
