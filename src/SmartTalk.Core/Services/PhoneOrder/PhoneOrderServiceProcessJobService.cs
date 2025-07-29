using Smarties.Messages.Enums.System;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.PhoneOrder;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.PhoneOrder;

namespace SmartTalk.Core.Services.PhoneOrder;

public interface IPhoneOrderServiceProcessJobService : IScopedDependency
{
    Task CalculatePhoneOrderRecodingDurationAsync(SchedulingCalculatePhoneOrderRecodingDurationCommand command, CancellationToken cancellationToken);
}

public class PhoneOrderServiceProcessJobService : IPhoneOrderServiceProcessJobService
{
    private readonly IFfmpegService _ffmpegService;
    private readonly IPhoneOrderDataProvider _phoneOrderDataProvider;
    private readonly ISmartTalkHttpClientFactory _smartTalkHttpClient;
    private readonly ISmartTalkBackgroundJobClient _smartTalkBackgroundJobClient;

    public PhoneOrderServiceProcessJobService(IFfmpegService ffmpegService, IPhoneOrderDataProvider phoneOrderDataProvider, ISmartTalkHttpClientFactory smartTalkHttpClient, ISmartTalkBackgroundJobClient smartTalkBackgroundJobClient)
    {
        _ffmpegService = ffmpegService;
        _smartTalkHttpClient = smartTalkHttpClient;
        _phoneOrderDataProvider = phoneOrderDataProvider;
        _smartTalkBackgroundJobClient = smartTalkBackgroundJobClient;
    }

    public async Task CalculatePhoneOrderRecodingDurationAsync(SchedulingCalculatePhoneOrderRecodingDurationCommand command, CancellationToken cancellationToken)
    {
        var (startTime, endTime) = GetQueryTimeRange();
        
        var records = await _phoneOrderDataProvider.GetPhoneOrderRecordsAsync(startTime: startTime, endTime: endTime, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (records == null || records.Count == 0) return;
        
        foreach (var record in records)
            _smartTalkBackgroundJobClient.Enqueue(() => CalculateRecordingDurationAsync(record, cancellationToken), HangfireConstants.InternalHostingPhoneOrder);
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetQueryTimeRange()
    {
        var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        
        var startLocal = new DateTime(2025, 7, 1, 0, 0, 0);
        var endLocal = new DateTime(2025, 7, 31, 23, 59, 59);
        
        var startInPst = new DateTimeOffset(startLocal, pacificZone.GetUtcOffset(startLocal));
        var endInPst = new DateTimeOffset(endLocal, pacificZone.GetUtcOffset(endLocal));
        
        return (startInPst.ToUniversalTime(), endInPst.ToUniversalTime());
    }

    public async Task CalculateRecordingDurationAsync(PhoneOrderRecord record, CancellationToken cancellationToken = default)
    {
        var audioBytes = await _smartTalkHttpClient.GetAsync<byte[]>(record.Url, cancellationToken).ConfigureAwait(false);
        
        var duration = await _ffmpegService.GetAudioDurationAsync(audioBytes, cancellationToken).ConfigureAwait(false);
        
        record.Duration = double.TryParse(duration, out var result) ? result : 0;
        
        await _phoneOrderDataProvider.UpdatePhoneOrderRecordsAsync(record, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}