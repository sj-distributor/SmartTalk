using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateOrderReservationInfoCommand : ICommand
{
    public int Id { get; set; }
    
    public int RecordId { get; set; }
    
    public string NotificationInfo { get; set; }

    public string AiNotificationInfo { get; set; }

    public string EnNotificationInfo { get; set; }
    
    public string EnAiNotificationInfo { get; set; }
}

public class UpdateOrderReservationInfoResponse : SmartTalkResponse<OrderReservationInfoDto>
{
} 