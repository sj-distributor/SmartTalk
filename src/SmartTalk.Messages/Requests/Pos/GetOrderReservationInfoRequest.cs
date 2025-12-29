using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetOrderReservationInfoRequest : IRequest
{
    public int OrderId { get; set; }
}

public class GetOrderReservationInfoResponse : SmartTalkResponse<OrderReservationInfoDto>
{
    
}

public class OrderReservationInfoDto
{
    public int Id { get; set; }
    
    public int RecordId { get; set; }
    
    public string NotificationInfo { get; set; }
    
    public string LastModifiedByName { get; set; }

    public string LastModifiedByPhone { get; set; }
    
    public CloudPrintStatus? CloudPrintStatus { get; set; }
    
    public Guid? CloudPrintOrderId { get; set; }
}