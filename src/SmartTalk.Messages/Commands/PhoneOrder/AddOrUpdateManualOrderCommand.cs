using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class AddOrUpdateManualOrderCommand : ICommand
{
    public string OrderId { get; set; }
    
    public int RecordId { get; set; }

    public string Restaurant { get; set; }
}

public class AddOrUpdateManualOrderResponse : SmartTalkResponse<List<PhoneOrderOrderItemDto>>
{
}