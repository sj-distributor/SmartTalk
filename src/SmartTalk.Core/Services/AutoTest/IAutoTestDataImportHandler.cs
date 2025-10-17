using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestDataImportHandler
{
    Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken = default); 
}

public class ExcelDataImportHandler : IAutoTestDataImportHandler
{
    public ExcelDataImportHandler()
    {
    }

    public async Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class ApiDataImportHandler : IAutoTestDataImportHandler
{
    public ApiDataImportHandler()
    {
    }
    
    public async Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class DbDataImportHandler : IAutoTestDataImportHandler
{
    public DbDataImportHandler()
    {
    }
    
    public async Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class CrawlDataImportHandler : IAutoTestDataImportHandler
{
    public CrawlDataImportHandler()
    {
    }
    
    public async Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class ScriptDataImportHandler : IAutoTestDataImportHandler
{
    public ScriptDataImportHandler()
    {
    }
    
    public async Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class ManualDataImportHandler : IAutoTestDataImportHandler
{
    public ManualDataImportHandler()
    {
    }
    
    public async Task ImportAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}