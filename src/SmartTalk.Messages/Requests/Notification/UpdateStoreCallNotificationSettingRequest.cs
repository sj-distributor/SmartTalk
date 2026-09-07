using System.ComponentModel.DataAnnotations;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Notification;

public class UpdateStoreCallNotificationSettingRequest: IRequest
{
    [Range(1, int.MaxValue)]
    public int StoreId { get; set; }

    [Required]
    public string ScenarioKey { get; set; }

    public bool IsEnabled { get; set; }
}

public class CallNotificationOperationResponse : SmartTalkResponse
{
}
