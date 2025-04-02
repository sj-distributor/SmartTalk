using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Linphone;

public class GetAgentBySipRequest : IRequest
{
    public List<string> Sips { get; set; }
}

public class GetAgentBySipResponse : SmartTalkResponse<List<GetAgentBySipDto>>
{
}

public class GetAgentBySipDto
{
    public int AgentId { get; set; }

    public string Restaurant { get; set; }
}