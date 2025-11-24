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
        var record = await _autoTestDataProvider.GetAutoTestImportDataRecordAsync(recordId, cancellationToken).ConfigureAwait(false);
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(scenarioId, cancellationToken).ConfigureAwait(false);

        try
        {
            List<AutoTestDataItem> matchedItems = null;

            switch (scenario.KeyName)
            {
                case "AiOrder":
                    matchedItems = await HandleAiOrderScenarioAsync(import, scenario, recordId, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    Log.Warning("未知 Scenario KeyName {KeyName}，跳过处理", scenario.KeyName);
                    break;
            }

            if (matchedItems == null || !matchedItems.Any())
            {
                record.Status = AutoTestStatus.Failed;
                await _autoTestDataProvider.UpdateAutoTestImportRecordAsync(record, true, cancellationToken).ConfigureAwait(false);
                Log.Information("Scenario {ScenarioId} 没有匹配的记录", scenarioId);
                return;
            }

            var setItems = matchedItems.Select(item => new AutoTestDataSetItem { DataSetId = dataSetId, DataItemId = item.Id, CreatedAt = DateTimeOffset.Now }).ToList();

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
        if (!import.TryGetValue("CustomerId", out var customerObj)) 
        { 
            Log.Warning("导入数据中缺少 customerId，跳过处理"); 
            return null; 
        } 
        var customerId = import["CustomerId"].ToString(); 
        var startDate = (DateTime)import["StartDate"]; 
        var endDate = (DateTime)import["EndDate"]; 
        
        var tokenResponse = await _ringCentralClient.TokenAsync(cancellationToken).ConfigureAwait(false); 
        var token = tokenResponse.AccessToken;
        
        var contacts = await _crmClient.GetCustomerContactsAsync(customerId, cancellationToken).ConfigureAwait(false); 
        var phoneNumbers = contacts.Select(c => NormalizePhone(c.Phone)).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        
        if (!phoneNumbers.Any()) 
        { 
            Log.Warning("客户 {CustomerId} 没有有效电话号码，结束处理", customerId); 
            return null; 
        }
        
        var allRecords = new List<RingCentralRecordDto>();
        
        foreach (var phone in phoneNumbers) 
        { 
            var monthTasks = new List<Task<List<RingCentralRecordDto>>>();
            
            var monthStart = new DateTime(startDate.Year, startDate.Month, 1); 
            var finalMonth = new DateTime(endDate.Year, endDate.Month, 1);
            
            int monthLimit = 0;
            
            while (monthStart <= finalMonth && monthLimit < 12) 
            { 
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                
                var from = monthStart < startDate ? startDate : monthStart; 
                var to = monthEnd > endDate ? endDate : monthEnd;
                
                monthTasks.Add(LoadOneMonthAsync(phone, token, from, to, cancellationToken));
                
                monthStart = monthStart.AddMonths(1); 
                monthLimit++; 
            }
            
            var monthResults = await Task.WhenAll(monthTasks).ConfigureAwait(false);
            
            foreach (var list in monthResults) 
                allRecords.AddRange(list); 
        }
        
        var singleCallNumbers = allRecords.GroupBy(r => new { Phone = NormalizePhone(r.From?.PhoneNumber ?? r.To?.PhoneNumber), Date = r.StartTime.Date })
            .Where(g => g.Count() == 1).Select(g => g.Key.Phone).ToList();
        
        if (!singleCallNumbers.Any())
        {
            Log.Warning("没有符合条件的单通话号码，跳过匹配");
            return null;
        }
        
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
                },cancellationToken), isRingCentralCall: false).ConfigureAwait(false);

            var sapOrders = sapResp?.Data?.RecordingData ?? new List<RecordingDataItem>();
            Log.Information("SAP 返回 {Count} 条订单记录, Customer={CustomerId}, Date={Date}", sapOrders.Count, customerId, sapStartDate);
            if (!sapOrders.Any()) return null;
            
            var oneOrderGroup = sapOrders.GroupBy(x => x.SalesDocument).SingleOrDefault();
            if (oneOrderGroup == null)
            {
                Log.Warning("SAP 没有找到唯一订单记录, Customer={CustomerId}", customerId);
                return null;
            }
            Log.Information("匹配成功: Customer={CustomerId}, Order={OrderId}, 项目数={ItemCount}", customerId, oneOrderGroup.Key, oneOrderGroup.Count());
            
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
    
    private async Task<T> RetryAsync<T>(Func<Task<T>> action, bool isRingCentralCall = false, int maxRetryCount = 2, int shortDelayMs = 2000)
    {
        int currentTry = 0;

        while (true)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                currentTry++;
                
                if (isRingCentralCall)
                {
                    if (currentTry > maxRetryCount) throw;
                    Log.Warning(ex, "RingCentral 调用失败，将在60秒后重试…");
                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
                else
                {
                    if (currentTry > maxRetryCount) throw;
                    Log.Warning(ex, "操作失败，将在 {Delay}ms 后重试…", shortDelayMs);
                    await Task.Delay(shortDelayMs);
                }
            }
        }
    }

    
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;
        
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 10) return "1" + digits;

        return digits;
    }
    
    private async Task<List<RingCentralRecordDto>> LoadOneMonthAsync(string phone, string token, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        return await RetryAsync(async () =>
        {
            Log.Information(
                "【LoadOneMonth】请求 RC 月度通话记录 Phone={Phone}, From={From}, To={To}",
                phone, from, to);

            var req = new RingCentralCallLogRequestDto
            {
                PhoneNumber = phone,
                DateFrom = from,
                DateTo = to,
                WithRecording = true,
                Page = 1,
                PerPage = 50 
            };

            var resp = await _ringCentralClient.GetRingCentralRecordAsync(req, token, cancellationToken).ConfigureAwait(false);

            return resp?.Records ?? new List<RingCentralRecordDto>();
        }, isRingCentralCall: true);
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