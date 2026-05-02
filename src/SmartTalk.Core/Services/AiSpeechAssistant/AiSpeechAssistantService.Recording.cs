using Serilog;
using Newtonsoft.Json.Linq;
using SmartTalk.Core.Services.Caching;
using SmartTalk.Core.Utils;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Dto.Attachments;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.Caching;
using SmartTalk.Messages.Enums.PhoneOrder;
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

        try
        {
            var speakInfos = await TranscribePhoneOrderSegmentsAsync(audioFileRawBytes, cancellationToken).ConfigureAwait(false);

            record.Status = speakInfos.Count == 0 ? PhoneOrderRecordStatus.NoContent : PhoneOrderRecordStatus.Diarization;
            record.TranscriptionJobId = null;

            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            if (speakInfos.Count != 0)
            {
                await _phoneOrderProcessJobService.HandleReleasedOpenAiDiarizedRecordingAsync(record, audioFileRawBytes, speakInfos, cancellationToken).ConfigureAwait(false);
            }

            await SendServerRestoreMessageIfNecessaryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            const string alertMessage = "服务器异常。";

            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, _workWeChatKeySetting.Key, alertMessage, mentionedList: new[]{"@all"}, cancellationToken: cancellationToken).ConfigureAwait(false);
            await _cacheManager.GetOrAddAsync("gpt-4o-audio-exception", _ => Task.FromResult(Task.FromResult(alertMessage)), new RedisCachingSetting(RedisServer.System, TimeSpan.FromDays(1)), cancellationToken).ConfigureAwait(false);

            record.Status = PhoneOrderRecordStatus.Exception;
            await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);

            Log.Error(e, "Handle ReceivePhoneRecord with OpenAI diarized transcription failed. CallSid: {CallSid}", command.CallSid);
        }
    }

    private async Task<List<SpeechMaticsSpeakInfoDto>> TranscribePhoneOrderSegmentsAsync(byte[] audioContent, CancellationToken cancellationToken)
    {
        Log.Information(
            "Starting OpenAI diarized transcription for phone call. FileSize: {FileSize}",
            audioContent?.Length ?? 0);

        var responseText = await _openaiClient
            .TranscribeDiarizedAudioAsync(audioContent, "recording.wav", cancellationToken)
            .ConfigureAwait(false);

        Log.Information(
            "OpenAI diarized transcription response received. BodyPreview: {BodyPreview}",
            BuildResponsePreview(responseText));

        if (string.IsNullOrWhiteSpace(responseText))
        {
            return [];
        }

        var payload = JObject.Parse(responseText);

        if (payload["error"] is JObject error)
        {
            throw new InvalidOperationException($"OpenAI diarized transcription request failed. Body={BuildResponsePreview(error.ToString())}");
        }

        var segments = payload["segments"] as JArray;

        if (segments == null || segments.Count == 0)
        {
            Log.Warning("OpenAI diarized transcription returned no segments. PayloadKeys: {Keys}", string.Join(", ", payload.Properties().Select(x => x.Name)));
            return [];
        }

        var speakInfos = segments
            .OfType<JObject>()
            .Select(x => new SpeechMaticsSpeakInfoDto
            {
                StartTime = x.Value<double?>("start") ?? 0,
                EndTime = x.Value<double?>("end") ?? 0,
                Speaker = x.Value<string>("speaker") ?? string.Empty,
                Text = x.Value<string>("text")?.Trim() ?? string.Empty
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        Log.Information("OpenAI diarized transcription parsed speakInfos: {@SpeakInfos}", speakInfos);

        return speakInfos;
    }

    private static string BuildResponsePreview(string responseText, int maxLength = 1200)
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
