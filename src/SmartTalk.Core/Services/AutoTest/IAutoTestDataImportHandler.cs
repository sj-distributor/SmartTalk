using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AutoTest.SalesAiOrder;
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
    private readonly IAutoTestSalesPhoneOrderProcessJobService _autoTestSalesPhoneOrderProcessJobService;

    public ApiDataImportHandler(IAutoTestDataProvider autoTestDataProvider, IAutoTestSalesPhoneOrderProcessJobService autoTestSalesPhoneOrderProcessJobService) 
    {
        _autoTestDataProvider = autoTestDataProvider;
        _autoTestSalesPhoneOrderProcessJobService = autoTestSalesPhoneOrderProcessJobService;
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, int scenarioId, int dataSetId, int recordId, CancellationToken cancellationToken = default)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(scenarioId, cancellationToken).ConfigureAwait(false);
        try
        {
            switch (scenario.KeyName)
            {
                case "AiOrder":
                    await _autoTestSalesPhoneOrderProcessJobService.CreateMonthlyJobsAsync(import, scenarioId, dataSetId, recordId, cancellationToken).ConfigureAwait(false);
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