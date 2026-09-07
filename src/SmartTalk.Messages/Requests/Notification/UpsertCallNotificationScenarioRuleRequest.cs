using System.ComponentModel.DataAnnotations;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Notification;

public class UpsertCallNotificationScenarioRuleRequest: IRequest
{
    [Required]
    public string ScenarioKey { get; set; }

    [Required]
    public string Description { get; set; }

    [Required]
    public string PromptTemplate { get; set; }

    public bool IsActive { get; set; } = true;
}