using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;
using SmartTalk.Messages.Dto.Linphone;

namespace SmartTalk.Messages.Requests.Linphone;

public class GetLinphoneHistoryRequest : IRequest
{
    public List<int> AgentId { get; set; }
}

public class GetLinphoneHistoryResponse : SmartTalkResponse<List<GetLinphoneHistoryDto>>
{
}