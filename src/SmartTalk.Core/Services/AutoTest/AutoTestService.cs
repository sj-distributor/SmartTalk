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
        
        var executionResult = await handlerInstance.ActionHandleAsync(scenario, cancellationToken).ConfigureAwait(false);
        
        return new AutoTestRunningResponse() { Data = executionResult };
    }
    
    private static readonly Dictionary<AutoTestRunningType, Type> HandlerTypeMap = new()
    {
        { AutoTestRunningType.AiOrder, typeof(AiOrderAutoTestHandler) },
    };
}