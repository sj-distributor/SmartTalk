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
    
    public string ReservationDate { get; set; }

    public string ReservationTime { get; set; }
    
    public string UserName { get; set; }
    
    public int? PartySize { get; set; }
    
    public string SpecialRequests { get; set; }

    public CloudPrintStatus? CloudPrintStatus { get; set; }
    
    public Guid? CloudPrintOrderId { get; set; }
}