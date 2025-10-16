using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetSurfaceAgentsRequest : IRequest
{
    public int PageIndex { get; set; } = 1;
    
    public int PageSize { get; set; } = 10;
    
    public string Keyword { get; set; }
    
    public List<int> AgentIds { get; set; }
}

public class GetSurfaceAgentsResponse : SmartTalkResponse<GetSurfaceAgentsResponseData>
{
}

public class GetSurfaceAgentsResponseData
{
    public int Count { get; set; }
    
    public List<AgentDto> Agents { get; set; }
}