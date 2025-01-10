using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderOrderItemsRequest : IRequest
{
    public int RecordId { get; set; }
}

public class GetPhoneOrderOrderItemsRessponse : SmartTalkResponse<GetPhoneOrderOrderItemsData>
{
}

public class GetPhoneOrderOrderItemsData
{
    public List<PhoneCallOrderItemDto> ManualItems { get; set; }
        
    public List<PhoneCallOrderItemDto> AIItems { get; set; }

    public string ManualOrderId { get; set; }
}