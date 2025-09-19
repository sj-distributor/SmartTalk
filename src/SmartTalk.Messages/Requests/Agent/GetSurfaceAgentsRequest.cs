using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetSurfaceAgentsRequest : IRequest
{
    public string Keyword { get; set; }
}

public class GetSurfaceAgentsResponse : SmartTalkResponse<List<AgentDto>>
{
}