using Mediator.Net.Contracts;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class UpdateOrderReservationInfoCommand : ICommand
{
    public int Id { get; set; }
    
    public int RecordId { get; set; }
    
    public string ReservationDate { get; set; }

    public string ReservationTime { get; set; }
    
    public string UserName { get; set; }
    
    public int? PartySize { get; set; }
    
    public string SpecialRequests { get; set; }
}

public class UpdateOrderReservationInfoResponse : SmartTalkResponse<OrderReservationInfoDto>
{
} 