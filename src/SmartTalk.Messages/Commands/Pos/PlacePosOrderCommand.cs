using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Pos;

public class PlacePosOrderCommand : ICommand
{
    public int OrderId { get; set; }
    
    public string OrderItems { get; set; }

    public bool IsWithRetry { get; set; } = true;
}

public class PlacePosOrderResponse : SmartTalkResponse<PosOrderDto>;