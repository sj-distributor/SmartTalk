using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestDataImportHandlerSwitcher : IScopedDependency
{
    IAutoTestDataImportHandler GetHandler(AutoTestImportDataRecordType importType);
}

public class AutoTestDataImportHandlerSwitcher : IAutoTestDataImportHandlerSwitcher
{
    private readonly Dictionary<AutoTestImportDataRecordType, IAutoTestDataImportHandler> _handlerMap;

    public AutoTestDataImportHandlerSwitcher(IEnumerable<IAutoTestDataImportHandler> handlers)
    {
        _handlerMap = handlers.ToDictionary(h => h.ImportType, h => h);
    }

    public IAutoTestDataImportHandler GetHandler(AutoTestImportDataRecordType importType)
    {
        if (!_handlerMap.TryGetValue(importType, out var handler))
            throw new NotSupportedException($"Unsupported import data type: {importType}");

        return handler;
    }
}
