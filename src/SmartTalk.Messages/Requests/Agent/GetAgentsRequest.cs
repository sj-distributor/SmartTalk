using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetAgentsRequest : HasServiceProviderId, IRequest
{
    public AgentType? AgentType { get; set; }
    
    public List<int> AgentIds { get; set; }
}

public class GetAgentsResponse : SmartTalkResponse<List<AgentPreviewDto>>
{
}