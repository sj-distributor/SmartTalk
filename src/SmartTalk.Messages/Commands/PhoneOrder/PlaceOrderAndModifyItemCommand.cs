using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneOrder;

public class PlaceOrderAndModifyItemCommand : ICommand
{
    public int RecordId { get; set; }
    
    public string CustomerName { get; set; }
    
    public string OrderPhoneNumber { get; set; }
    
    public List<PhoneOrderOrderItemDto> OrderItems { get; set; }
}

public class PlaceOrderAndModifyItemResponse : SmartTalkResponse<PlaceOrderAndModifyItemResponseData>
{
}

public class PlaceOrderAndModifyItemResponseData
{
    public string OrderNumber { get; set; }
    
    public List<PhoneOrderOrderItemDto> OrderItems { get; set; }
}