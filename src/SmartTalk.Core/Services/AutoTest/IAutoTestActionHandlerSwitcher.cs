using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestActionHandlerSwitcher: IScopedDependency
{
    IAutoTestActionHandler GetHandler(AutoTestActionType type);
}

public class AutoTestActionHandlerSwitcher : IAutoTestActionHandlerSwitcher
{
    private readonly Dictionary<AutoTestActionType, IAutoTestActionHandler> _handlerMap;

    public AutoTestActionHandlerSwitcher(IEnumerable<IAutoTestActionHandler> handlers)
    {
        _handlerMap = handlers.ToDictionary(h => h.ActionType, h => h);
    }

    public IAutoTestActionHandler GetHandler(AutoTestActionType runningType)
    {
        if (!_handlerMap.TryGetValue(runningType, out var handler))
            throw new NotSupportedException($"Unsupported import data type: {runningType}");

        return handler;
    }
}
