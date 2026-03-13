using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Pos;

public class GetStoreByAgentIdRequest : IRequest
{
    public int AgentId { get; set; }
}

public class GetStoreByAgentIdResponse : SmartTalkResponse<int?>
{
}