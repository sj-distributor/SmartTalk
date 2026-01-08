using Serilog;
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
using SmartTalk.Core.Services.Printer;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Commands.PhoneOrder;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SmartTalk.Core.Services.PhoneOrder;

public partial interface IPhoneOrderProcessJobService : IScopedDependency
{
    Task CalculatePhoneOrderRecodingDurationAsync(SchedulingCalculatePhoneOrderRecodingDurationCommand command, CancellationToken cancellationToken);

    Task CalculateRecordingDurationAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken = default);
}

public partial class PhoneOrderProcessJobService : IPhoneOrderProcessJobService
{
    private readonly IPosService  _posService;
    private readonly ISalesClient _salesClient;
    private readonly IFfmpegService _ffmpegService;
    private readonly OpenAiSettings _openAiSettings;
    private readonly TwilioSettings _twilioSettings;
    private readonly ISmartiesClient _smartiesClient;
    private readonly IPosUtilService _posUtilService;
    private readonly IPosDataProvider _posDataProvider;
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
        IPosService posService,
        ISalesClient salesClient,
        IFfmpegService ffmpegService,
        TwilioSettings twilioSettings,
        OpenAiSettings openAiSettings,
        ISmartiesClient smartiesClient,
        IPosUtilService posUtilService,
        IPosDataProvider posDataProvider,
        TranslationClient translationClient,
        IPhoneOrderService phoneOrderService,
        ISalesDataProvider salesDataProvider,
        IPhoneOrderDataProvider phoneOrderDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClient,
        ISmartTalkBackgroundJobClient backgroundJobClient,
        ISpeechMaticsDataProvider speechMaticsDataProvider,
        ISmartTalkHttpClientFactory smartTalkHttpClientFactory,
        ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient,
        IAiSpeechAssistantDataProvider aiSpeechAssistantDataProvider, IPhoneOrderUtilService phoneOrderUtilService)
    {
        _posService = posService;
        _salesClient = salesClient;
        _ffmpegService = ffmpegService;
        _twilioSettings = twilioSettings;
        _openAiSettings = openAiSettings;
        _smartiesClient = smartiesClient;
        _posUtilService = posUtilService;
        _posDataProvider = posDataProvider;
        _translationClient = translationClient;
        _phoneOrderService = phoneOrderService;
        _salesDataProvider = salesDataProvider;
        _smartTalkHttpClient = smartTalkHttpClient;
        _backgroundJobClient = backgroundJobClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _speechMaticsDataProvider = speechMaticsDataProvider;
        _smartTalkHttpClientFactory = smartTalkHttpClientFactory;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
        _aiSpeechAssistantDataProvider = aiSpeechAssistantDataProvider;
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
        Log.Information("Ready calculate the record: {@Record}", record);

        if (record.Duration is > 0)
        {
            Log.Information("Record don't need to be calculated: {@Record}", record);
            return;
        }
        
        var audioBytes = audioContent == null || audioContent.Length == 0
            ? await _smartTalkHttpClient.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false)
            : audioContent;
        
        var duration = await _ffmpegService.GetAudioDurationAsync(audioBytes, cancellationToken).ConfigureAwait(false);
        
        Log.Information("Calculating recording duration: {Duration}", duration);
        
        record.Duration = TimeSpan.TryParse(duration, out var timeSpan) ? timeSpan.TotalSeconds : 0;
    }

    public async Task FillingIncomingCallNumberAsync(PhoneOrderRecord record, CancellationToken cancellationToken)
    {
        if (record.Url.Contains("twilio") && string.IsNullOrWhiteSpace(record.IncomingCallNumber))
        {
            TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

            var call = await CallResource.FetchAsync(record.SessionId);
        
            record.IncomingCallNumber = call?.From ?? string.Empty;
        }
    }
}