using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneOrderRecordsRequest : IRequest
{
    public PhoneOrderRestaurant Restaurant { get; set; }
}

public class GetPhoneOrderRecordsResponse : SmartTalkResponse<List<PhoneOrderRecordDto>>
{
}