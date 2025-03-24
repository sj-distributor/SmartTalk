using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Linphone;

namespace SmartTalk.Messages.Requests.Linphone;

public class GetLinphoneHistoryDetailsRequest : IRequest
{
    public string Targetter { get; set; }
}

public class GetLinphoneHistoryDetailsResponse : SmartTalkResponse<List<GetLinphoneHistoryDto>>
{
}