using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

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
    
    public ApiDataImportHandler()
    {
    }
    
    public async Task ImportAsync(Dictionary<string, object> import, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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