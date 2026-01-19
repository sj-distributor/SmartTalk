using System.Text;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Constants;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestProcessJobService : IScopedDependency
{
    Task SyncCallRecordsAsync(SyncCallRecordCommand command, CancellationToken cancellationToken);
    
    Task SyncCallRecordsByWindowAsync(DateTime startTimeUtc, DateTime endTimeUtc, CancellationToken cancellationToken);
}

public class AutoTestProcessJobService : IAutoTestProcessJobService
{
    private readonly ICrmClient _crmClient;
    private readonly IAttachmentService _attachmentService;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public AutoTestProcessJobService(ICrmClient crmClient, IAttachmentService attachmentService, IAutoTestDataProvider autoTestDataProvider, 
        ISmartTalkHttpClientFactory httpClientFactory, ISmartTalkBackgroundJobClient backgroundJobClient)
    {
        _crmClient = crmClient;
        _attachmentService = attachmentService;
        _httpClientFactory = httpClientFactory;
        _backgroundJobClient = backgroundJobClient;
        _autoTestDataProvider = autoTestDataProvider;
    }
    
    public async Task SyncCallRecordsAsync(SyncCallRecordCommand command, CancellationToken cancellationToken)
    {
        var whiteList = await _autoTestDataProvider.GetCustomerPhoneWhiteListAsync(cancellationToken).ConfigureAwait(false);

        if (whiteList == null || whiteList.Count == 0)
            return;

        var now = DateTime.UtcNow.AddDays(-3);

        var globalStartTime = new DateTime(2025, 9, 9, 0, 0, 0, DateTimeKind.Utc);

        foreach (var phone in whiteList)
        {
            var lastRecord = await _autoTestDataProvider.GetLastCallRecordAsync(phone, cancellationToken).ConfigureAwait(false);

            if (lastRecord == null)
            {
                globalStartTime = new DateTime(2025, 9, 9, 0, 0, 0, DateTimeKind.Utc);
                break;
            }

            if (lastRecord.StartTimeUtc < globalStartTime)
                globalStartTime = lastRecord.StartTimeUtc;
        }

        var windowSize = TimeSpan.FromHours(12);
        var startTime = globalStartTime;

        while (startTime < now)
        {
            var endTime = startTime + windowSize;
            if (endTime > now) endTime = now;

            Log.Information("Enqueue SyncCallRecordsByWindowAsync: {StartTime} - {EndTime}", startTime, endTime);

            _backgroundJobClient.Enqueue<IAutoTestProcessJobService>(x => x.SyncCallRecordsByWindowAsync(startTime, endTime, CancellationToken.None), HangfireConstants.InternalHostingAutoTestCallRecordSync);

            startTime = endTime;
        }
    }

    public async Task SyncCallRecordsByWindowAsync(DateTime startTimeUtc, DateTime endTimeUtc, CancellationToken cancellationToken)
    {
        var records = await _crmClient.GetCallRecordsAsync(startTimeUtc, endTimeUtc, cancellationToken).ConfigureAwait(false);

        if (records == null || records.Count == 0) 
            return;

        var callLogIds = records.Where(x => x.Source == 0 && !string.IsNullOrEmpty(x.Id)).Select(x => x.Id).Distinct().ToList();

        var existingIds = await _autoTestDataProvider.GetExistingCallLogIdsAsync(callLogIds, cancellationToken).ConfigureAwait(false);

        var existingSet = existingIds.ToHashSet();

        var recordsToInsert = new List<AutoTestCallRecordSync>();

        foreach (var record in records)
        {
            if (record.Source != 0)
                continue;

            if (string.IsNullOrEmpty(record.Id))
                continue;

            if (existingSet.Contains(record.Id))
                continue;

            var from = NormalizePhone(record.From);
            var to = NormalizePhone(record.To);

            string recordingUrl = null;
            if (!string.IsNullOrEmpty(record.RecordingUrl))
            {
                recordingUrl = await UploadRecordingToOssAsync(record.RecordingUrl, cancellationToken).ConfigureAwait(false);
                Log.Information("Uploaded recording for record {Id} to {RecordingUrl}", record.Id, recordingUrl);
            }

            recordsToInsert.Add(new AutoTestCallRecordSync
            {
                CallLogId = record.Id,
                CallId = record.CallId,
                FromNumber = from,
                ToNumber = to,
                Direction = record.Direction,
                ExtensionId = record.ExtensionId,
                StartTimeUtc = record.StartTime,
                RecordingUrl = recordingUrl,
                Source = record.Source,
                LastUpdated = DateTime.UtcNow
            });
        }

        if (recordsToInsert.Count > 0)
        {
            await _autoTestDataProvider.InsertCallRecordsAsync(recordsToInsert, true, cancellationToken).ConfigureAwait(false);
        }
    }
    
    private async Task<string> UploadRecordingToOssAsync(string recordingUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(recordingUrl))
            return null;
        
        var fileBytes = await _httpClientFactory.GetAsync<byte[]>(recordingUrl, cancellationToken);

        if (fileBytes == null || fileBytes.Length == 0)
            return null;

        var fileName = Guid.NewGuid() + ".mp3";
        
        var ossResponse = await _attachmentService.UploadAttachmentAsync(new UploadAttachmentCommand
        {
            Attachment = new UploadAttachmentDto
            {
                FileName = fileName,
                FileContent = fileBytes
            }
        }, cancellationToken).ConfigureAwait(false);

        return ossResponse.Attachment?.FileUrl;
    }
    
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        var sb = new StringBuilder();
        foreach (var ch in phone)
        {
            if (char.IsDigit(ch) || ch == '+')
                sb.Append(ch);
        }

        return sb.ToString();
    }
}