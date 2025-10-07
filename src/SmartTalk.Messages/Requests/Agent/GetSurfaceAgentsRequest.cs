using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetSurfaceAgentsRequest : IRequest
{
    public int PageIndex { get; set; } = 1;
    
    public int PageSize { get; set; } = 100;
    
    public string Keyword { get; set; }
}

public class GetSurfaceAgentsResponse : SmartTalkResponse<List<AgentDto>>
{
}