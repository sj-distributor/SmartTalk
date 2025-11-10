using Newtonsoft.Json;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Dto.AutoTest;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Core.Services.AutoTest;

public interface IAutoTestActionHandler : IScopedDependency
{
    AutoTestActionType ActionType { get; }
    
    Task ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default);
}

public class ApiAutoTestHandler : IAutoTestActionHandler
{
    // API
    public AutoTestActionType ActionType => AutoTestActionType.Api;
    
    // SalesOrder scenario
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
        
        var taskRecords = await _autoTestDataProvider.GetStatusTaskRecordsByTaskIdAsync(taskId, AutoTestTaskRecordStatus.Pending, cancellationToken).ConfigureAwait(false);
        
        foreach (var record in taskRecords)
        {
            record.Status = AutoTestTaskRecordStatus.Ongoing;
            
            await _autoTestDataProvider.UpdateTaskRecordsAsync(taskRecords, cancellationToken: cancellationToken).ConfigureAwait(false);
            
            // TODO: 执行API请求
        }
    }
}