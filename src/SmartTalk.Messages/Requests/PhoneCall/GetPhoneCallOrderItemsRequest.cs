using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneCall;

public class GetPhoneCallOrderItemsRequest : IRequest
{
    public int RecordId { get; set; }
}

public class GetPhoneCallOrderItemsRessponse : SmartTalkResponse<GetPhoneCallOrderItemsData>
{
}

public class GetPhoneCallOrderItemsData
{
    public List<PhoneCallOrderItemDto> ManualItems { get; set; }
        
    public List<PhoneCallOrderItemDto> AIItems { get; set; }

    public string ManualOrderId { get; set; }
}