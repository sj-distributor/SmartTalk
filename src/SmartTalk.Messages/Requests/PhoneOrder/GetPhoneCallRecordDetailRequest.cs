using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.PhoneOrder;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.PhoneOrder;

public class GetPhoneCallRecordDetailRequest : IRequest
{
    public int Month { get; set; }
    
    public bool IncludeExternalData { get; set; } = false;
}

public class GetPhoneCallRecordDetailResponse : SmartTalkResponse<string>;