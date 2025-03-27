using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Linphone;

namespace SmartTalk.Messages.Requests.Linphone;

public class GetLinphoneHistoryDetailsRequest : IRequest
{
    public string Caller { get; set; }
}

public class GetLinphoneHistoryDetailsResponse : SmartTalkResponse<List<LinphoneHistoryDto>>
{
}