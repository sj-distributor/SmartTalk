using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.Restaurant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetAgentsRequest : IRequest
{
    public AgentType AgentType { get; set; }
}

public class GetAgentsResponse : SmartTalkResponse<List<GetAgentsResponseData>>
{
}

public class GetAgentsResponseData
{
    public AgentDto Agent { get; set; }
    
    public RestaurantDto Restaurant { get; set; }
}