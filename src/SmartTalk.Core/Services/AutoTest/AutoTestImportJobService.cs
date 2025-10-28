using System.Text.Json;
using Serilog;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.RingCentral;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestImportJobService : IScopedDependency
{
    Task<int> HandleAutoTestImportDataJobAsync(string customerId, DateTime startDate, DateTime endDate, int scenarioId, CancellationToken cancellationToken);
}

public class AutoTestImportJobService : IAutoTestImportJobService
{
    private readonly ICrmClient _crmClient;
    private readonly ISapGatewayClients _sapGatewayClient;
    private readonly IRingCentralClient _ringCentralClient;
    private readonly IAutoTestDataProvider _autoTestDataProvider;

    public AutoTestImportJobService(ICrmClient crmClient, ISapGatewayClients sapGatewayClient, IRingCentralClient ringCentralClient, IAutoTestDataProvider autoTestDataProvider)
    {
        _crmClient = crmClient;
        _sapGatewayClient = sapGatewayClient;
        _ringCentralClient = ringCentralClient;
        _autoTestDataProvider = autoTestDataProvider;
    }

    public async Task<int> HandleAutoTestImportDataJobAsync(string customerId, DateTime startDate, DateTime endDate, int scenarioId, CancellationToken cancellationToken)
    {
        Log.Information("开始处理客户 {CustomerId} 的录音导入任务。", customerId);
        
        var tokenResponse = await _ringCentralClient.TokenAsync(cancellationToken).ConfigureAwait(false);
        var token = tokenResponse.AccessToken;
        
        var contacts = await _crmClient.GetCustomerContactsAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (contacts == null || !contacts.Any())
        {
            Log.Warning("客户 {CustomerId} 无联系人，任务结束。", customerId);
            return 0;
        }

        var phoneNumbers = contacts.Select(c => c.Phone).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();

        if (!phoneNumbers.Any())
        {
            Log.Warning("客户 {CustomerId} 没有有效电话号码，任务结束。", customerId);
            return 0;
        }
        
        var allRecords = new List<RingCentralRecordDto>();
        foreach (var phone in phoneNumbers)
        {
            var rcRequest = new RingCentralCallLogRequestDto
            {
                PhoneNumber = phone,
                DateFrom = startDate,
                DateTo = endDate,
                WithRecording = true,
                Page = 1,
                PerPage = 100
            };

            try
            {
                var rcResponse = await _ringCentralClient.GetRingCentralRecordAsync(rcRequest, token, cancellationToken);
                if (rcResponse?.Records != null) allRecords.AddRange(rcResponse.Records);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "查询客户 {CustomerId} 电话 {PhoneNumber} 录音失败。", customerId, phone);
            }
        }
        
        var singleCallNumbers = allRecords.GroupBy(r => r.From?.PhoneNumber ?? r.To?.PhoneNumber).Where(g => g.Count() == 1)
            .Select(g => g.Key).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        if (!singleCallNumbers.Any())
        {
            Log.Information("客户 {CustomerId} 没有一天仅一通电话的录音，任务结束。", customerId);
            return 0;
        }

        var sapRequest = new QueryRecordingDataRequest
        {
            CustomerId = new List<string> { customerId },
            StartDate = startDate,
            EndDate = endDate,
            PageNumber = 1,
            PageSize = 200
        };

        var sapResponse = await _sapGatewayClient.QueryRecordingDataAsync(sapRequest, cancellationToken).ConfigureAwait(false);
        
        var importRecord = new AutoTestImportDataRecord
        {
            ScenarioId = scenarioId,
            Type = AutoTestImportDataRecordType.Api,
            Status = AutoTestStatus.Pending,
            OpConfig = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "CustomerId", customerId },
                { "StartDate", startDate },
                { "EndDate", endDate },
                { "PhoneNumbers", singleCallNumbers }
            }),
            CreatedAt = DateTimeOffset.Now
        };

        await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false);
        
        var dataItems = sapResponse.Data.RecordingData.Select(dataItem => new AutoTestDataItem
        {
            ScenarioId = scenarioId,
            ImportRecordId = importRecord.Id,
            InputJson = JsonSerializer.Serialize(dataItem),
            CreatedAt = DateTimeOffset.Now
        }).ToList();

        await _autoTestDataProvider.AddAutoTestDataItemsAsync(dataItems, true, cancellationToken).ConfigureAwait(false);

        Log.Information("客户 {CustomerId} 导入任务完成，共 {Count} 条数据。", customerId, dataItems.Count);

        return importRecord.Id;
    }
}