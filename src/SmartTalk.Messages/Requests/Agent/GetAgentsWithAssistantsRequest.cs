using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetAgentsWithAssistantsRequest : IRequest
{
    public int? StoreId { get; set; }
}

public class GetAgentsWithAssistantsResponse : SmartTalkResponse<List<AgentDto>>
{
}