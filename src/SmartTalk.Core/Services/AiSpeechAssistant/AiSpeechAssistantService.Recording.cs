using Serilog;
using OpenAI.Chat;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.STT;
using Task = System.Threading.Tasks.Task;

namespace SmartTalk.Core.Services.AiSpeechAssistant;

public partial interface IAiSpeechAssistantService
{
    Task RecordAiSpeechAssistantCallAsync(RecordAiSpeechAssistantCallCommand command, CancellationToken cancellationToken);

    Task ReceivePhoneRecordingStatusCallbackAsync(ReceivePhoneRecordingStatusCallbackCommand command, CancellationToken cancellationToken);
}

public partial class AiSpeechAssistantService
{
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

        var (record, agent) = await RetryHelper.RetryOnResultAsync(
            ct => _phoneOrderDataProvider.GetRecordWithAgentAsync(command.CallSid, ct),
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
            language = await DetectAudioLanguageAsync(audioFileRawBytes, cancellationToken).ConfigureAwait(false);

            await SendServerRestoreMessageIfNecessaryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            const string alertMessage = "服务器异常。";

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, alertMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
            await _cacheManager.GetOrAddAsync("gpt-4o-audio-exception", _ => Task.FromResult(Task.FromResult(alertMessage)), new RedisCachingSetting(RedisServer.System, TimeSpan.FromDays(1)), cancellationToken).ConfigureAwait(false);
        }

        record.Language = ConvertLanguageCode(language);
        record.TranscriptionJobId = await _phoneOrderService.CreateSpeechMaticsJobAsync(audioFileRawBytes, Guid.NewGuid().ToString("N") + ".wav", language, cancellationToken).ConfigureAwait(false);

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

    private async Task<string> DetectAudioLanguageAsync(byte[] audioContent, CancellationToken cancellationToken)
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

        ChatCompletion completion = await client.CompleteChatAsync(messages, options, cancellationToken);

        Log.Information("Detect the audio language: " + completion.Content.FirstOrDefault()?.Text);

        return completion.Content.FirstOrDefault()?.Text ?? "en";
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