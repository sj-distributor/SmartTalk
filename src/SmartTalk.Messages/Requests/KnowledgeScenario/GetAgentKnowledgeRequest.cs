using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.KnowledgeScenario;

public class GetAgentKnowledgeRequest : IRequest
{
    public int StoreId { get; set; }

    public string Keyword { get; set; }
}

public class GetAgentKnowledgeResponse : SmartTalkResponse<List<GetAgentKnowledgeResponseData>>
{
}

public class GetAgentKnowledgeResponseData
{
    public int AgentId { get; set; }
    
    public string AgentName { get; set; }

    public List<AssistantDto> Assistants { get; set; }
}

public class AssistantDto
{
    public int AssistantId { get; set; }

    public string AssistantName { get; set; }
}
