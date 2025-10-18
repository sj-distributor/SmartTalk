using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService
{
    Task<AutoTestImportDataResponse> AutoTestImportDataAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken);
}

public partial class AutoTestService
{
    public async Task<AutoTestImportDataResponse> AutoTestImportDataAsync(AutoTestImportDataCommand command, CancellationToken cancellationToken)
    {
        var handler = _autoTestDataImportHandlerSwitcher.GetHandler(command.ImportType);
       
        await handler.ImportAsync(command, cancellationToken);
        
        return new AutoTestImportDataResponse();
    }
    
    private static readonly Dictionary<AutoTestImportDataRecordType, Type> ImportDataHandlerTypeMap = new()
    {
        { AutoTestImportDataRecordType.Excel, typeof(ExcelDataImportHandler) },
        { AutoTestImportDataRecordType.Api, typeof(ApiDataImportHandler) },
        { AutoTestImportDataRecordType.Db, typeof(DbDataImportHandler) },
        { AutoTestImportDataRecordType.Crawl, typeof(CrawlDataImportHandler) },
        { AutoTestImportDataRecordType.Script, typeof(ScriptDataImportHandler) },
        { AutoTestImportDataRecordType.Manual, typeof(ManualDataImportHandler) },
    };
}