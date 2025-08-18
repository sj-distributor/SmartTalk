using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Core.Settings.Twilio;
using SmartTalk.Messages.Commands.PhoneOrder;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SmartTalk.Core.Services.PhoneOrder;

public interface IPhoneOrderProcessJobService : IScopedDependency
{
    Task CalculatePhoneOrderRecodingDurationAsync(SchedulingCalculatePhoneOrderRecodingDurationCommand command, CancellationToken cancellationToken);

    Task CalculateRecordingDurationAsync(PhoneOrderRecord record, byte[] audioContent, CancellationToken cancellationToken = default);
}

public class PhoneOrderProcessJobService : IPhoneOrderProcessJobService
{
    private readonly IFfmpegService _ffmpegService;
    private readonly TwilioSettings _twilioSettings;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClient;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;

    public PhoneOrderProcessJobService(IFfmpegService ffmpegService, TwilioSettings twilioSettings, IPhoneOrderDataProvider phoneOrderDataProvider, ISmartTalkHttpClientFactory smartTalkHttpClient, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _ffmpegService = ffmpegService;
        _twilioSettings = twilioSettings;
        _smartTalkHttpClient = smartTalkHttpClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
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