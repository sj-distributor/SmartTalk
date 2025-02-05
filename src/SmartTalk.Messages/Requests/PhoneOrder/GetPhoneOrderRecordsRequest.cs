using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewPhoneOrder })]
public class GetPhoneOrderRecordsRequest : IRequest
{
    public int RestaurantId { get; set; }
}

public class GetPhoneOrderRecordsResponse : SmartTalkResponse<List<PhoneOrderRecordDto>>
{
}