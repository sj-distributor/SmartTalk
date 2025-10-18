using AutoMapper;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.AutoTest.AiOrder;
using SmartTalk.Messages.Commands.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public partial interface IAutoTestService : IScopedDependency
{
    Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken);
}

public partial class AutoTestService : IAutoTestService
{
    private readonly IMapper _mapper;
    private readonly IAutoTestDataProvider _autoTestDataProvider;

    public AutoTestService(IMapper mapper, IAutoTestDataProvider autoTestDataProvider)
    {
        _mapper = mapper;
        _autoTestDataProvider = autoTestDataProvider;
    }
    
    public async Task<AutoTestRunningResponse> AutoTestRunningAsync(AutoTestRunningCommand command, CancellationToken cancellationToken)
    {
        var scenario = await _autoTestDataProvider.GetAutoTestScenarioByIdAsync(command.ScenarioId, cancellationToken).ConfigureAwait(false);
        
        if (!HandlerTypeMap.TryGetValue(command.TestRunningType, out var handlerType)) throw new NotSupportedException($"not support auto test project type {command.TestRunningType}");
        
        var handlerInstance = Activator.CreateInstance(handlerType) as IAutoTestRunningHandler;

        var inputHandleAsyncMethod = handlerType.GetMethod("InputHandleAsync", new Type[] { typeof(AutoTestScenario), typeof(CancellationToken) });
        
        var inputTask = (Task<string>)inputHandleAsyncMethod.Invoke(handlerInstance, new object[] { scenario, cancellationToken });
        
        var inputJson = await inputTask.ConfigureAwait(false);
        
        var actionHandleAsyncMethod = handlerType.GetMethod("ActionHandleAsync", new Type[] { typeof(AutoTestScenario), typeof(string), typeof(CancellationToken) });
        
        var actionTask = (Task<string>)actionHandleAsyncMethod.Invoke(handlerInstance, new object[] { scenario, inputJson, cancellationToken });
        
        var executionResult = await actionTask.ConfigureAwait(false);
        
        return new AutoTestRunningResponse() { Data = executionResult };
    }
    
    private static readonly Dictionary<AutoTestRunningType, Type> HandlerTypeMap = new()
    {
        { AutoTestRunningType.AiOrder, typeof(AiOrderAutoTestHandler) },
    };
}