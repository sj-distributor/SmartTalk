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
        var promptText = import.ContainsKey("PromptText") ? import["PromptText"]?.ToString():"";
        
        var importRecord = new AutoTestImportDataRecord 
        { 
            ScenarioId = scenarioId, 
            Type = AutoTestImportDataRecordType.Api, 
            Status = AutoTestStatus.Running, 
            OpConfig = JsonSerializer.Serialize(import), 
            CreatedAt = DateTimeOffset.Now 
        }; 
        await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false);
        
        try 
        { 
            var tokenResponse = await _ringCentralClient.TokenAsync(cancellationToken).ConfigureAwait(false); 
            var token = tokenResponse.AccessToken;
            
            var contacts = await _crmClient.GetCustomerContactsAsync(customerId, cancellationToken).ConfigureAwait(false); 
            var phoneNumbers = contacts.Select(c => NormalizePhone(c.Phone)).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            
            if (!phoneNumbers.Any()) 
            { 
                Log.Warning("客户 {CustomerId} 没有有效电话号码，任务结束。", customerId); 
                importRecord.Status = AutoTestStatus.Done; 
                await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken); 
                return; 
            }
            
            var allRecords = new List<RingCentralRecordDto>(); 
            var ringCentralTasks = phoneNumbers.Select(phone =>
            {
                return RetryAsync(async () =>
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

                    return new { phone, response = resp };

                });
            }).ToList();

            var results = await Task.WhenAll(ringCentralTasks).ConfigureAwait(false);

            foreach (var result in results)
            {
                if (result?.response?.Records != null)
                    allRecords.AddRange(result.response.Records);
            }
            
            var singleCallNumbers = allRecords.GroupBy(r => r.From?.PhoneNumber ?? r.To?.PhoneNumber).Where(g => g.Count() == 1)
                .Select(g => g.Key).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            
            if (!singleCallNumbers.Any()) 
            { 
                Log.Information("客户 {CustomerId} 没有一天仅一通电话的录音，任务结束。", customerId); 
                importRecord.Status = AutoTestStatus.Done; 
                await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken); 
                return; 
            }
            
            var tasks = singleCallNumbers.Select(phone => allRecords.First(r => (r.From?.PhoneNumber ?? r.To?.PhoneNumber) == phone))
                .Select(record => MatchOrderAndRecordingAsync(customerId, record, scenarioId, importRecord.Id, promptText, cancellationToken)).ToList();
            
            var matchedItems = (await Task.WhenAll(tasks)).Where(x => x != null).ToList()!;
            
            if (matchedItems.Any()) 
            { 
                await _autoTestDataProvider.AddAutoTestDataItemsAsync(matchedItems, true, cancellationToken).ConfigureAwait(false); 
            }
            
            importRecord.Status = AutoTestStatus.Done; 
            await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false); 
        }
        catch (Exception ex) 
        { 
            Log.Error(ex, "ImportAsync 失败 {CustomerId}", customerId); 
            importRecord.Status = AutoTestStatus.Failed; 
            await _autoTestDataProvider.AddAutoTestImportRecordAsync(importRecord, true, cancellationToken).ConfigureAwait(false); 
        } 
    }
    
    private async Task<AutoTestDataItem> MatchOrderAndRecordingAsync(string customerId, RingCentralRecordDto record, int scenarioId, int importRecordId, string promptText, CancellationToken cancellationToken)
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
                PromptText = promptText,
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