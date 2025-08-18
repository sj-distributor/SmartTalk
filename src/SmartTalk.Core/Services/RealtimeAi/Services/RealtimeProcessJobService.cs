using Google.Cloud.Translation.V2;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Agents;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.PhoneOrder;
using SmartTalk.Core.Services.Security;
using SmartTalk.Core.Services.STT;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Enums.STT;

namespace SmartTalk.Core.Services.RealtimeAi.Services;

public interface IRealtimeProcessJobService : IScopedDependency
{
    Task RecordingRealtimeAiAsync(string recordingUrl, int agentId, CancellationToken cancellationToken);
}

public class RealtimeProcessJobService : IRealtimeProcessJobService
{
    private readonly TranslationClient _translationClient;
    private readonly IAgentDataProvider _agentDataProvider;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly ISpeechToTextService _speechToTextService;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;

    public RealtimeProcessJobService(
        TranslationClient translationClient,
        IAgentDataProvider agentDataProvider,
        IPhoneOrderService phoneOrderService,
        ISpeechToTextService speechToTextService,
        ISmartTalkHttpClientFactory httpClientFactory,
        ISecurityDataProvider securityDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider)
    {
        _phoneOrderService = phoneOrderService;
        _agentDataProvider = agentDataProvider;
        _translationClient = translationClient;
        _httpClientFactory = httpClientFactory;
        _speechToTextService = speechToTextService;
        _phoneOrderDataProvider = phoneOrderDataProvider;
    }

    public async Task RecordingRealtimeAiAsync(string recordingUrl, int agentId, CancellationToken cancellationToken)
    {
        var agent = await _agentDataProvider.GetAgentByIdAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (agent is { IsSendAudioRecordWechat: true })
            await _phoneOrderService.SendWorkWeChatRobotNotifyAsync(null, agent.WechatRobotKey, $"您有一条新的AI通话录音：\n{recordingUrl}", [], CancellationToken.None).ConfigureAwait(false);
        
        var recordingContent = await _httpClientFactory.GetAsync<byte[]>(recordingUrl, cancellationToken).ConfigureAwait(false);
        if (recordingContent == null) return;
        
        var transcription = await _speechToTextService.SpeechToTextAsync(
            recordingContent, fileType: TranscriptionFileType.Wav, responseFormat: TranscriptionResponseFormat.Text, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        var detection = await _translationClient.DetectLanguageAsync(transcription, cancellationToken).ConfigureAwait(false);
        
        var record = new PhoneOrderRecord { SessionId = Guid.NewGuid().ToString(), AgentId = agentId, Url = recordingUrl, Language = SelectLanguageEnum(detection.Language), CreatedDate = DateTimeOffset.Now, Status = PhoneOrderRecordStatus.Recieved };
        
        await _phoneOrderDataProvider.AddPhoneOrderRecordsAsync([record], cancellationToken: cancellationToken).ConfigureAwait(false);
        
        record.TranscriptionJobId = await _phoneOrderService.CreateSpeechMaticsJobAsync(recordingContent, Guid.NewGuid().ToString("N") + ".wav", detection.Language, cancellationToken).ConfigureAwait(false);
        record.Status = PhoneOrderRecordStatus.Diarization;
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, true, cancellationToken).ConfigureAwait(false);
    }

    private TranscriptionLanguage SelectLanguageEnum(string language)
    {
        return language switch
        {
            "zh" or "zh-CN" or "zh-TW" => TranscriptionLanguage.Chinese,
            _ => TranscriptionLanguage.English
        };
    }
}