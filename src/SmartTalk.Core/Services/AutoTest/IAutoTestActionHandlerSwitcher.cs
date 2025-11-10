using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestActionHandlerSwitcher: IScopedDependency
{
    IAutoTestActionHandler GetHandler(AutoTestActionType type, string scenarioName);
}

public class AutoTestActionHandlerSwitcher : IAutoTestActionHandlerSwitcher
{
    private readonly Dictionary<(AutoTestActionType, string), IAutoTestActionHandler> _handlerMap;

    public AutoTestActionHandlerSwitcher(IEnumerable<IAutoTestActionHandler> handlers)
    {
        _handlerMap = new Dictionary<(AutoTestActionType, string), IAutoTestActionHandler>();

        foreach (var handler in handlers)
        {
            var key = (handler.ActionType, handler.ScenarioName?.Trim() ?? string.Empty);

            if (!_handlerMap.TryAdd(key, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate handler registration detected for type '{handler.ActionType}' and scenario '{handler.ScenarioName}'.");
            }
        }
    }

    public IAutoTestActionHandler GetHandler(AutoTestActionType runningType, string scenarioName)
    {
        var key = (runningType, scenarioName?.Trim() ?? string.Empty);
        
        if (!_handlerMap.TryGetValue(key, out var handler))
            throw new NotSupportedException($"No handler found for type '{runningType}' and scenario '{scenarioName}'.");

        return handler;
    }
}
