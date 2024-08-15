using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests;

public class GetPhoneOrderOrderItemsRequest : IRequest
{
    public int RecordId { get; set; }
}

public class GetPhoneOrderOrderItemsRessponse : SmartTalkResponse<GetPhoneOrderOrderItemsData>
{
}

public class GetPhoneOrderOrderItemsData
{
    public List<PhoneOrderOrderItemDto> ManualItems { get; set; }
        
    public List<PhoneOrderOrderItemDto> AIItems { get; set; }
}