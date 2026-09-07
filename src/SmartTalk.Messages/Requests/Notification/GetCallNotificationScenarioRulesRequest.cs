using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Notification;

public class GetCallNotificationScenarioRulesRequest: IRequest
{
    public int? StoreId { get; set; }
}

public class CallNotificationScenarioRulesResponse : SmartTalkResponse<List<CallNotificationScenarioRuleDto>>
{
}

public class CallNotificationScenarioRuleDto
{
    public string ScenarioKey { get; set; }
    public string ScenarioName { get; set; }
    public string Description { get; set; }
    public string PromptTemplate { get; set; }
    public bool IsActive { get; set; }
    public bool IsEnabled { get; set; }
}