using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestActionHandler : IScopedDependency
{
    AutoTestActionType ActionType { get; }
    
    public string ScenarioName => "";
    
    Task ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default);
}

public class ApiAutoTestHandler : IAutoTestActionHandler
{
    public AutoTestActionType ActionType => AutoTestActionType.Api;
    
    public string ScenarioName => "AiOrder";
    
    private readonly IAutoTestDataProvider _autoTestDataProvider;
    
    public ApiAutoTestHandler(IAutoTestDataProvider autoTestDataProvider)
    {
        _autoTestDataProvider = autoTestDataProvider;
    }
    
    public async Task ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenario.ActionConfig)) throw new Exception("ActionConfig is empty");
        
        var actionConfig = JsonConvert.DeserializeObject<AutoTestSalesOrderActionConfigDto>(scenario.ActionConfig);
        
        Log.Information("ApiAutoTestHandler ActionHandleAsync actionConfig:{@actionConfig}", actionConfig);
        
        // TODO: 执行API请求
    }
}