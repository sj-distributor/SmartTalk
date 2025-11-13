using System.Text;
using Newtonsoft.Json;
using Serilog;
using Smarties.Messages.Responses;
using SmartTalk.Core.Domain.AutoTest;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http;
using SmartTalk.Messages.Commands.AutoTest.SalesPhoneOrder;
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
    private readonly ISmartiesHttpClientFactory _httpClientFactory;
    
    public ApiAutoTestHandler(IAutoTestDataProvider autoTestDataProvider, ISmartiesHttpClientFactory httpClientFactory)
    {
        _autoTestDataProvider = autoTestDataProvider;
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task ActionHandleAsync(AutoTestScenario scenario, int taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scenario.ActionConfig)) throw new Exception("ActionConfig is empty");
        
        var actionConfig = JsonConvert.DeserializeObject<AutoTestSalesOrderActionConfigDto>(scenario.ActionConfig);
        
        Log.Information("ApiAutoTestHandler ActionHandleAsync actionConfig:{@actionConfig}", actionConfig);
        
        var body = JsonConvert.DeserializeObject<ExecuteSalesPhoneOrderTestCommand>(actionConfig.Body);

        body.TaskId = taskId;
        
        await ExecuteAsync(actionConfig, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task ExecuteAsync(AutoTestSalesOrderActionConfigDto config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            throw new ArgumentException("Url 不能为空");
        
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(config.Headers))
        {
            try
            {
                headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(config.Headers);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Headers 必须是 JSON 格式，例如：{{\"Authorization\":\"Bearer token\"}}，错误详情：{ex.Message}");
            }
        }

        var method = (config.HttpMethod ?? "GET").Trim().ToUpperInvariant();
        HttpResponseMessage response;

        switch (method)
        {
            case "GET":
                response = await _httpClientFactory.GetAsync<HttpResponseMessage>(config.Url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;

            case "POST":
                await _httpClientFactory.PostAsJsonAsync(config.Url, config.Body ?? "", headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;
            
            default:
                throw new NotSupportedException($"暂不支持的 HttpMethod: {config.HttpMethod}");
        }
    }
}

