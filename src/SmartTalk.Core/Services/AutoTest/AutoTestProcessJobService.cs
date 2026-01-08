using System.Text;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Attachments;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Commands.Attachments;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Dto.Attachments;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestProcessJobService : IScopedDependency
{
    Task SyncCallRecordsAsync(SyncCallRecordCommand command, CancellationToken cancellationToken);
}

public class AutoTestProcessJobService : IAutoTestProcessJobService
{
    private readonly ICrmClient _crmClient;
    private readonly IAttachmentService _attachmentService;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public AutoTestProcessJobService(ICrmClient crmClient, IAttachmentService attachmentService, IAutoTestDataProvider autoTestDataProvider, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _crmClient = crmClient;
        _attachmentService = attachmentService;
        _httpClientFactory = httpClientFactory;
        _autoTestDataProvider = autoTestDataProvider;
    }
    
    public async Task SyncCallRecordsAsync(SyncCallRecordCommand command, CancellationToken cancellationToken)
    {
        var whiteList = await _autoTestDataProvider.GetCustomerPhoneWhiteListAsync(cancellationToken).ConfigureAwait(false);
        
        var lastRecord = await _autoTestDataProvider.GetLastCallRecordAsync(cancellationToken).ConfigureAwait(false);
        var startTime = lastRecord?.StartTimeUtc ?? new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = DateTime.UtcNow;
        
        var records = await _crmClient.GetCallRecordsAsync(startTime, endTime, cancellationToken).ConfigureAwait(false);

        var recordsToInsert = new List<AutoTestCallRecordSync>();

        foreach (var record in records)
        {
            if (record.Source != 0) continue;

            var from = NormalizePhone(record.From);
            var to = NormalizePhone(record.To);

            if (!whiteList.Contains(from) && !whiteList.Contains(to))
                continue;

            var recordingUrl = string.IsNullOrEmpty(record.RecordingUrl) ? null : await UploadRecordingToOssAsync(record.RecordingUrl, cancellationToken).ConfigureAwait(false);

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

        if (recordsToInsert.Any())
        {
            await _autoTestDataProvider.InsertCallRecordsAsync(recordsToInsert, true, cancellationToken).ConfigureAwait(false);
        }
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
}