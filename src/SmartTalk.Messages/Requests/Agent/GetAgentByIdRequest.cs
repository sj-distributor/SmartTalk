using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetAgentByIdRequest : IRequest
{
    public int AgentId { get; set; }
}

public class GetAgentByIdResponse : SmartTalkResponse<AgentDto>
{
}