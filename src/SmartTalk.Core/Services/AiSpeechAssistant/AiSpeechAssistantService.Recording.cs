using Serilog;
using Lingua;
using OpenAI.Audio;
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
}

public partial class AiSpeechAssistantService
{
    private static readonly Regex TranscriptTokenRegex = new(
        @"[\u3400-\u4DBF\u4E00-\u9FFF\uF900-\uFAFF]+|[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7AF]+|[\p{L}]+(?:['’-][\p{L}]+)*",
        RegexOptions.Compiled);

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

        var (record, agent, _) = await RetryHelper.RetryOnResultAsync(
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
            language = await DetectAudioLanguageFromTranscriptAsync(audioFileRawBytes, cancellationToken).ConfigureAwait(false);

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

    private async Task<string> DetectAudioLanguageFromTranscriptAsync(byte[] audioContent, CancellationToken cancellationToken)
    {
        var transcript = await TranscribeAudioAsync(audioContent, cancellationToken).ConfigureAwait(false);
        var languageCode = DetectDominantLanguageFromTranscript(transcript);

        Log.Information("Detect audio language by transcript. TranscriptLength: {TranscriptLength}, Result: {Result}",
            transcript?.Length ?? 0, languageCode);

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

    private string DetectDominantLanguageFromTranscript(string transcript)
    {
        var detector = LanguageDetectorBuilder
            .FromLanguages(Language.English, Language.Spanish, Language.Korean, Language.Chinese)
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
            return RefineChineseLanguageCode(MapLinguaLanguageToCode(detector.DetectLanguageOf(transcript)) ?? "en", transcript);
        }

        var dominantLanguageCode = weights
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .FirstOrDefault() ?? "en";

        return RefineChineseLanguageCode(dominantLanguageCode, transcript);
    }

    private string MapLinguaLanguageToCode(Language language)
    {
        return language switch
        {
            Language.English => "en",
            Language.Spanish => "es",
            Language.Korean => "ko",
            Language.Chinese => "zh",
            _ => null
        };
    }

    private string RefineChineseLanguageCode(string languageCode, string transcript)
    {
        if (!string.Equals(languageCode, "zh", StringComparison.OrdinalIgnoreCase))
        {
            return languageCode;
        }

        if (ContainsCantoneseMarkers(transcript))
        {
            return "zh";
        }

        return ContainsTraditionalChineseMarkers(transcript) ? "zh-TW" : "zh-CN";
    }

    private bool ContainsCantoneseMarkers(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }

        var markers = new[]
        {
            "佢", "佢哋", "冇", "咗", "喺", "嘅", "咩", "啲", "唔", "嚟", "呢", "嗰", "咁", "噉", "而家", "邊個", "乜嘢"
        };

        return markers.Any(transcript.Contains);
    }

    private bool ContainsTraditionalChineseMarkers(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }

        var markers = new[]
        {
            "這", "個", "們", "來", "還", "說", "點", "裡", "為", "會", "讓", "買", "賣", "應", "該", "覺", "關", "係",
            "處", "龍", "體", "醫", "藥", "臺", "灣", "與", "對", "於", "經", "過", "聽", "訊", "錄"
        };

        return markers.Any(transcript.Contains);
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
