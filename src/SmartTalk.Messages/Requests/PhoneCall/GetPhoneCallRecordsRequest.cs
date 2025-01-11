using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.PhoneCall;
using SmartTalk.Messages.Enums.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneCall;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewPhoneOrder })]
public class GetPhoneCallRecordsRequest : IRequest
{
    public PhoneCallRestaurant Restaurant { get; set; }
}

public class GetPhoneCallRecordsResponse : SmartTalkResponse<List<PhoneCallRecordDto>>
{
}