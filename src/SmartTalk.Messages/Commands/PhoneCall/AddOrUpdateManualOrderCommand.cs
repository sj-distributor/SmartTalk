using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Enums.PhoneCall;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class AddOrUpdateManualOrderCommand : ICommand
{
    public string OrderId { get; set; }
    
    public int RecordId { get; set; }

    public PhoneCallRestaurant Restaurant { get; set; }
}

public class AddOrUpdateManualOrderResponse : SmartTalkResponse<List<PhoneCallOrderItemDto>>
{
}