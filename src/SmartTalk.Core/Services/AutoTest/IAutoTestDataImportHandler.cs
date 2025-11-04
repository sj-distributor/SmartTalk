using System.Text.Json;
using Serilog;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http.Clients;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Dto.RingCentral;
using SmartTalk.Messages.Enums.AutoTest;
using SmartTalk.Messages.Requests.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestDataImportHandler : IScopedDependency
{
    AutoTestImportDataRecordType ImportType { get; }
    
    Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken = default); 
}

public class ExcelDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Excel;
    
    public ExcelDataImportHandler()
    {
    }

    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class ApiDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Api;
    private readonly ISapGatewayClients _sapGatewayClient;
    private readonly ICrmClient _crmClient;
    private readonly IRingCentralClient _ringCentralClient;
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    
    public ApiDataImportHandler(ISapGatewayClients sapGatewayClient, ICrmClient crmClient, IRingCentralClient ringCentralClient, IAutoTestDataProvider autoTestDataProvider)
    {
        _sapGatewayClient = sapGatewayClient;
        _crmClient = crmClient;
        _ringCentralClient = ringCentralClient;
        _autoTestDataProvider = autoTestDataProvider;
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        var customerId = import["CustomerId"].ToString();
        var startDate = (DateTime)import["StartDate"];
        var endDate = (DateTime)import["EndDate"];
        var scenarioId = Convert.ToInt32(import["ScenarioId"]);

        Log.Information("开始处理客户 {CustomerId} 的录音导入任务。", customerId);
        
        var sapRequest = new QueryRecordingDataRequest
        {
            CustomerId = new List<string> { customerId },
            StartDate = startDate,
            EndDate = endDate,
            PageNumber = 1,
            PageSize = 200
        };
        var sapResponse = await _sapGatewayClient.QueryRecordingDataAsync(sapRequest, cancellationToken);
        var sapOrders = sapResponse.Data?.RecordingData ?? new List<RecordingDataItem>();

        if (!sapOrders.Any())
        {
            Log.Information("客户 {CustomerId} 在 {StartDate}~{EndDate} 没有订单，任务结束。", customerId, startDate, endDate);
            return;
        }
        
        var contacts = await _crmClient.GetCustomerContactsAsync(customerId, cancellationToken);
        var phoneNumbers = contacts?.Select(c => c.Phone).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();

        if (phoneNumbers == null || !phoneNumbers.Any())
        {
            Log.Warning("客户 {CustomerId} 没有有效电话号码，任务结束。", customerId);
            return;
        }

        var tokenResponse = await _ringCentralClient.TokenAsync(cancellationToken).ConfigureAwait(false);
        var token = tokenResponse.AccessToken;
        
        RingCentralRecordDto uniqueCall = null;
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
                var records = rcResponse?.Records ?? new List<RingCentralRecordDto>();

                if (records.Count == 1)
                {
                    uniqueCall = records.First();
                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "查询客户 {CustomerId} 电话 {PhoneNumber} 录音失败。", customerId, phone);
            }
        }

        if (uniqueCall == null)
        {
            Log.Information("客户 {CustomerId} 当天没有唯一通话，任务结束。", customerId);
            return;
        }
        
        var importRecord = new AutoTestImportDataRecord
        {
            ScenarioId = scenarioId,
            Type = AutoTestImportDataRecordType.Api,
            Status = AutoTestStatus.Running,
            OpConfig = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "CustomerId", customerId },
                { "StartDate", startDate },
                { "EndDate", endDate }
            }),
            CreatedAt = DateTimeOffset.Now
        };

        await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken);
        
        var grouped = sapOrders.GroupBy(x => x.SalesDocument);

        var dataItems = grouped.Select(g =>
        {
            var inputDto = new AutoTestInputJsonDto
            {
                Recording = uniqueCall.Recording?.Uri ?? string.Empty,
                OrderId = g.Key,
                CustomerId = customerId,
                Detail = g.Select((i, index) => new AutoTestInputDetail
                {
                    SerialNumber = index + 1,
                    Quantity = i.Qty,
                    ItemDesc = i.Description?.ToString() ?? ""
                }).ToList()
            };

            return new AutoTestDataItem
            {
                ScenarioId = scenarioId,
                ImportRecordId = importRecord.Id,
                InputJson = JsonSerializer.Serialize(inputDto),
                CreatedAt = DateTimeOffset.Now
            };
        }).ToList();

        await _autoTestDataProvider.AddAutoTestDataItemsAsync(dataItems, true, cancellationToken);

        Log.Information("客户 {CustomerId} 导入任务完成，共 {Count} 条数据。", customerId, dataItems.Count);
    }
}

public class DbDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Db;
    
    public DbDataImportHandler()
    {
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class CrawlDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Crawl;
    
    public CrawlDataImportHandler()
    {
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class ScriptDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Script;
    
    public ScriptDataImportHandler()
    {
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class ManualDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Manual;
    
    public ManualDataImportHandler()
    {
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}