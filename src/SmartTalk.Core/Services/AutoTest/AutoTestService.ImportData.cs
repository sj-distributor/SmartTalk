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
        if (!ImportDataHandlerTypeMap.TryGetValue(command.ImportType, out var handlerType)) throw new NotSupportedException($"not support auto test import data type {command.ImportType}");
        
        var handlerInstance = Activator.CreateInstance(handlerType) as IAutoTestDataImportHandler;

        var handleAsyncMethod = handlerType.GetMethod("ImportAsync", new Type[] { typeof(AutoTestScenario), typeof(CancellationToken) });
        
        var task = (Task)handleAsyncMethod.Invoke(handlerInstance, new object[] { command, cancellationToken });
        
        await task.ConfigureAwait(false);
        
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