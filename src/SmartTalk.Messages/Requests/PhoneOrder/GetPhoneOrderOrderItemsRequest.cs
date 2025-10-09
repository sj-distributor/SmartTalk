using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderOrderItemsRequest : IRequest
{
    public int RecordId { get; set; }
}

public class GetPhoneOrderOrderItemsResponse : SmartTalkResponse<GetPhoneOrderOrderItemsData>
{
}

public class GetPhoneOrderOrderItemsData
{
    public List<PhoneOrderOrderItemDto> ManualItems { get; set; }
        
    public List<PhoneOrderOrderItemDto> AIItems { get; set; }

    public string ManualOrderId { get; set; }
    
    public string CustomerName { get; set; }
    
    public string OrderPhoneNumber { get; set; }
}