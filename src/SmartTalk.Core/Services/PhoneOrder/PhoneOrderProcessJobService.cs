using Google.Cloud.Translation.V2;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Services.Pos;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Core.Services.Twilio;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderProcessJobService : IScopedDependency
{
    Task CalculatePhoneOrderRecodingDurationAsync(SchedulingCalculatePhoneOrderRecodingDurationCommand command, CancellationToken cancellationToken);

    Task CalculateRecordingDurationAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken = default);
}

public partial class PhoneOrderProcessJobService : IPhoneOrderProcessJobService
{
    private readonly ISalesClient _salesClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly ITwilioService _twilioService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly IPosUtilService _posUtilService;
    private readonly ISmartiesClient _smartiesClient;
    private readonly TranslationClient _translationClient;
    private readonly IPhoneOrderService _phoneOrderService;
    private readonly ISalesDataProvider _salesDataProvider;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClient;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClientFactory;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;
    private readonly IAiSpeechAssistantDataProvider _aiSpeechAssistantDataProvider;
    private readonly IPhoneOrderUtilService _phoneOrderUtilService;

    public PhoneOrderProcessJobService(
        ISalesClient salesClient,
        IFfmpegService ffmpegService, 
        ITwilioService twilioService,
        TwilioSettings twilioSettings,
        OpenAiSettings openAiSettings,
        ISmartiesClient smartiesClient,
        TranslationClient translationClient,
        IPhoneOrderService phoneOrderService,
        ISalesDataProvider salesDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClient,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, IPosUtilService posUtilService, IPhoneOrderUtilService phoneOrderUtilService)
    {
        _salesClient = salesClient;
        _ffmpegService = ffmpegService;
        _twilioService = twilioService;
        _twilioSettings = twilioSettings;
        _openAiSettings = openAiSettings;
        _smartiesClient = smartiesClient;
        _translationClient = translationClient;
        _phoneOrderService = phoneOrderService;
        _salesDataProvider = salesDataProvider;
        _smartTalkHttpClient = smartTalkHttpClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _smartTalkBackgroundJobClient = backgroundJobClient;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
        _posUtilService = posUtilService;
        _phoneOrderUtilService = phoneOrderUtilService;
    }

    public async Task CalculatePhoneOrderRecodingDurationAsync(SchedulingCalculatePhoneOrderRecodingDurationCommand command, CancellationToken cancellationToken)
    {
        var (startTime, endTime) = GetQueryTimeRange();
        
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(startTime: startTime, endTime: endTime, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (records == null || records.Count == 0) return;
        
        foreach (var record in records.Where(x => !string.IsNullOrWhiteSpace(x.Url)))
            _smartTalkBackgroundJobClient.Enqueue(() => CalculateRecordingDurationAsync(record, null, cancellationToken), HangfireConstants.InternalHostingFfmpeg);
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetQueryTimeRange()
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        
        var startLocal = new DateTime(2025, 8, 1, 0, 0, 0);
        var endLocal = new DateTime(2025, 8, 31, 23, 59, 59);
        
        var startInPst = new DateTimeOffset(startLocal, pacificZone.GetUtcOffset(startLocal));
        var endInPst = new DateTimeOffset(endLocal, pacificZone.GetUtcOffset(endLocal));
        
        return (startInPst.ToUniversalTime(), endInPst.ToUniversalTime());
    }

    public async Task CalculateRecordingDurationAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken = default)
    {
        await FillingIncomingCallNumberAsync(record, cancellationToken).ConfigureAwait(false);
        
        await CalculateRecordingDurationInternalAsync(record, audioContent, cancellationToken).ConfigureAwait(false);
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task CalculateRecordingDurationInternalAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken = default)
    {
        if (record.Duration is > 0) return;
        
        var audioBytes = audioContent == null || audioContent.Length == 0
            ? await _smartTalkHttpClient.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false)
            : audioContent;
        
        var duration = await _ffmpegService.GetAudioDurationAsync(audioBytes, cancellationToken).ConfigureAwait(false);
        
        record.Duration = TimeSpan.TryParse(duration, out var timeSpan) ? timeSpan.TotalSeconds : 0;
    }

    public async Task FillingIncomingCallNumberAsync(PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (record.Url.Contains("twilio") && string.IsNullOrWhiteSpace(record.IncomingCallNumber))
        {
            var callInfo = await _twilioService.FetchCallAsync(record.SessionId);

            record.IncomingCallNumber = callInfo?.From ?? string.Empty;
        }
    }
}