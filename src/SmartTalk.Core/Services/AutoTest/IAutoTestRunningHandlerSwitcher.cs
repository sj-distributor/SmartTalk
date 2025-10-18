using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestRunningHandlerSwitcher: IScopedDependency
{
    IAutoTestRunningHandler GetHandler(AutoTestRunningType type);
}

public class AutoTestRunningHandlerSwitcher : IAutoTestRunningHandlerSwitcher
{
    private readonly Dictionary<AutoTestRunningType, IAutoTestRunningHandler> _handlerMap;

    public AutoTestRunningHandlerSwitcher(IEnumerable<IAutoTestRunningHandler> handlers)
    {
        _handlerMap = handlers.ToDictionary(h => h.RunningType, h => h);
    }

    public IAutoTestRunningHandler GetHandler(AutoTestRunningType runningType)
    {
        if (!_handlerMap.TryGetValue(runningType, out var handler))
            throw new NotSupportedException($"Unsupported import data type: {runningType}");

        return handler;
    }
}
