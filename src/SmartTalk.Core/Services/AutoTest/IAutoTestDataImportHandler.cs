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
    
    Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken = default); 
}

public class ExcelDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Excel;
    
    public ExcelDataImportHandler()
    {
    }

    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken)
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
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken = default)
    { 
        // 1. 获取AutoTestScenario通过scenarioId
        // 2. 如果没有就直接return
        // 3. 目前理论只有AiOrder，然后专门有个方法是处理这个场景的
        // 4. 方法返回的是matchedItems
        // 5. 统一add matchedItems 以及add 到set中
        
        var record = await _autoTestDataProvider.GetAutoTestImportDataRecordAsync(recordId, cancellationToken).ConfigureAwait(false);
        
        // 1. 获取 AutoTestScenario 通过 scenarioId
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(scenarioId, cancellationToken).ConfigureAwait(false);
        if (scenario == null)
        {
            // 2. 如果没有就直接 return
            Log.Warning("Scenario {ScenarioId} 不存在，跳过导入。", scenarioId);
            record.Status = AutoTestStatus.Failed;
            await _autoTestDataProvider.UpdateAutoTestImportRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            List<AutoTestDataItem> matchedItems = null;

            // 3. 按 KeyName 分场景处理
            switch (scenario.KeyName)
            {
                case "AiOrder":
                    matchedItems = await HandleAiOrderScenarioAsync(import, scenario, recordId, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    Log.Warning("未知 Scenario KeyName {KeyName}，跳过处理", scenario.KeyName);
                    break;
            }

            // 4. 方法返回的是 matchedItems
            if (matchedItems == null || !matchedItems.Any())
            {
                record.Status = AutoTestStatus.Failed;
                await _autoTestDataProvider.UpdateAutoTestImportRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
                Log.Information("Scenario {ScenarioId} 没有匹配的记录", scenarioId);
                return;
            }

            var setItems = matchedItems.Select(item => new AutoTestDataSetItem { DataSetId = dataSetId, DataItemId = item.Id, CreatedAt = DateTimeOffset.Now }).ToList();

            // 5. 统一add matchedItems 以及add 到set中
            await _autoTestDataProvider.AddAutoTestDataItemsAsync(matchedItems, true, cancellationToken).ConfigureAwait(false);
            if (setItems.Any())
                await _autoTestDataProvider.AddAutoTestDataSetItemsAsync(setItems, cancellationToken).ConfigureAwait(false); 
            
            record.Status = AutoTestStatus.Done;
            await _autoTestDataProvider.UpdateAutoTestImportRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImportAsync 失败 ScenarioId={ScenarioId}", scenarioId);
        }
    }

    private async Task<List<AutoTestDataItem>> HandleAiOrderScenarioAsync(Dictionary<string, object> import, AutoTestScenario scenario, int recordId, CancellationToken cancellationToken)
    {
        if (!import.TryGetValue("customerId", out var customerObj)) 
        { 
            Log.Warning("导入数据中缺少 customerId，跳过处理"); 
            return null; 
        } 
        var customerId = customerObj.ToString();
        
        DateTime startDate = import.TryGetValue("startDate", out var startObj) && DateTime.TryParse(startObj?.ToString(), out var s) ? s : DateTime.Now.Date;
        DateTime endDate   = import.TryGetValue("endDate", out var endObj) && DateTime.TryParse(endObj?.ToString(), out var e) ? e : startDate.AddDays(1);
        
        var tokenResponse = await _ringCentralClient.TokenAsync(cancellationToken).ConfigureAwait(false); 
        var token = tokenResponse.AccessToken;
        
        var contacts = await _crmClient.GetCustomerContactsAsync(customerId, cancellationToken).ConfigureAwait(false); 
        var phoneNumbers = contacts.Select(c => c.Phone).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        
        if (!phoneNumbers.Any()) 
        { 
            Log.Warning("客户 {CustomerId} 没有有效电话号码，结束处理", customerId); 
            return null; 
        }
        
        var allRecords = new List<RingCentralRecordDto>();
        var tasks = phoneNumbers.Select(phone => RetryAsync(async () =>
        {
            var rcRequest = new RingCentralCallLogRequestDto
            {
                PhoneNumber = phone,
                DateFrom = startDate,
                DateTo = endDate,
                WithRecording = true,
                Page = 1,
                PerPage = 200
            };

            var resp = await _ringCentralClient.GetRingCentralRecordAsync(rcRequest, token, cancellationToken).ConfigureAwait(false);
            return resp?.Records ?? new List<RingCentralRecordDto>();
        })).ToList();
        
        var results = await Task.WhenAll(tasks); 
        foreach (var records in results) 
            allRecords.AddRange(records);
        
        var singleCallNumbers = allRecords.GroupBy(r => r.From?.PhoneNumber ?? r.To?.PhoneNumber).Where(g => g.Count() == 1)
            .Select(g => g.Key).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        
        if (!singleCallNumbers.Any()) return null;
        
        var matchedTasks = singleCallNumbers.Select(phone => allRecords.First(r => NormalizePhone(r.From?.PhoneNumber ?? r.To?.PhoneNumber) == phone))
            .Select(record => MatchOrderAndRecordingAsync(customerId, record, scenario.Id, recordId, cancellationToken)).ToList();
        
        var matchedItems = (await Task.WhenAll(matchedTasks)).Where(x => x != null).ToList();
        
        return matchedItems;
    }

    private async Task<AutoTestDataItem> MatchOrderAndRecordingAsync(string customerId, RingCentralRecordDto record, int scenarioId, int importRecordId, CancellationToken cancellationToken)
    {
        try
        {
            var sapStartDate = record.StartTime.Date;
            var sapEndDate = sapStartDate.AddDays(1);

            var sapResp = await RetryAsync(() 
                => _sapGatewayClient.QueryRecordingDataAsync(new QueryRecordingDataRequest
                {
                    CustomerId = new List<string> { customerId },
                    StartDate = sapStartDate,
                    EndDate = sapEndDate
                }, cancellationToken)).ConfigureAwait(false);

            var sapOrders = sapResp?.Data?.RecordingData ?? new List<RecordingDataItem>();
            if (!sapOrders.Any()) return null;
            
            var oneOrderGroup = sapOrders.GroupBy(x => x.SalesDocument).SingleOrDefault();
            if (oneOrderGroup == null) return null;

            var inputJsonDto = new AutoTestInputJsonDto
            {
                Recording = record.Recording?.Uri ?? "",
                OrderId = oneOrderGroup.Key,
                CustomerId = customerId,
                Detail = oneOrderGroup.Select((i, index) => new AutoTestInputDetail
                {
                    SerialNumber = index + 1,
                    Quantity = i.Qty,
                    ItemName = i.Description ?? "",
                    ItemId = i.Material
                }).ToList()
            };

            return new AutoTestDataItem
            {
                ScenarioId = scenarioId,
                ImportRecordId = importRecordId,
                InputJson = JsonSerializer.Serialize(inputJsonDto),
                CreatedAt = DateTimeOffset.Now
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "匹配订单和录音失败 {CustomerId}", customerId);
            return null;
        }
    }
    
    private async Task<T> RetryAsync<T>(Func<Task<T>> action, int retryCount = 2, int delayMs = 2000)
    {
        int currentTry = 0;
        while (true)
        {
            try
            {
                return await action();
            }
            catch
            {
                currentTry++;
                if (currentTry > retryCount) throw;
                await Task.Delay(delayMs);
            }
        }
    }
    
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";
        
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 10) return "1" + digits;

        return digits;
    }
}

public class DbDataImportHandler : IAutoTestDataImportHandler
{
    public AutoTestImportDataRecordType ImportType => AutoTestImportDataRecordType.Db;
    
    public DbDataImportHandler()
    {
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken)
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
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken)
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
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken)
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
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}