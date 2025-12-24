using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetStoreSurfaceAgentsRequest : IRequest
{
    public List<int> AgentIds { get; set; }
}

public class GetStoreSurfaceAgentsResponse :  SmartTalkResponse<GetStoreSurfaceAgentsResponseData>
{
}

public class GetStoreSurfaceAgentsResponseData
{
    public int Count { get; set; }
    
    public List<AgentDto> Agents { get; set; }
}