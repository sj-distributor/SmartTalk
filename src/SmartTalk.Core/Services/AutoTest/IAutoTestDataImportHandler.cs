using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AutoTest.SalesAiOrder;
using SmartTalk.Core.Services.Jobs;
using SmartTalk.Messages.Enums.AutoTest;

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
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    private readonly ISmartTalkBackgroundJobClient _backgroundJobClient;

    public ApiDataImportHandler(IAutoTestDataProvider autoTestDataProvider, ISmartTalkBackgroundJobClient backgroundJobClient) 
    {
        _autoTestDataProvider = autoTestDataProvider;
        _backgroundJobClient = backgroundJobClient;
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken = default)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(scenarioId, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (scenario.KeyName)
            {
                case "AiOrder":
                    if (!import.TryGetValue("CustomerId", out var customerId) || !import.TryGetValue("StartDate", out var startDateObj) || !import.TryGetValue("EndDate", out var endDateObj))
                        return;
                    
                    DateTime startUtc = startDateObj is DateTime dt1 ? dt1 : DateTime.Parse(startDateObj.ToString());
                    DateTime endUtc = endDateObj is DateTime dt2 ? dt2 : DateTime.Parse(endDateObj.ToString());
                    
                    var cursor = new DateTime(startUtc.Year, startUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    while (cursor <= endUtc)
                    {
                        var monthStart = cursor < startUtc ? startUtc : cursor;
                        var monthEnd = cursor.AddMonths(1).AddDays(-1).Date.AddDays(1).AddMilliseconds(-1);
                        if (monthEnd > endUtc) monthEnd = endUtc;

                        _backgroundJobClient.Enqueue<IAutoTestSalesPhoneOrderProcessJobService>(x =>
                            x.ProcessPartialRecordingOrderMatchingAsync(scenarioId, dataSetId, recordId, monthStart, monthEnd, customerId.ToString(), cancellationToken));

                        cursor = cursor.AddMonths(1);
                    }
                    break;

                default:
                    Log.Warning("未知 Scenario KeyName {KeyName}，跳过处理", scenario.KeyName);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ImportAsync 失败 ScenarioId={ScenarioId}", scenarioId);
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

        public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId,
            CancellationToken cancellationToken)
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

        public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId,
            CancellationToken cancellationToken)
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

        public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}