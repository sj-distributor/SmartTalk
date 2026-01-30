using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetPosStoreOrderRequest : IRequest
{
    public int? OrderId { get; set; }
    
    public int? RecordId { get; set; }
}

public class GetPosStoreOrderResponse : SmartTalkResponse<PosOrderDto>;