using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.Agent;

public class GetAutoTestAgentAndAssistantsRequest : IRequest
{
    public int? StoreId { get; set; }
    
    public int? AgentId { get; set; }
    
    public int? AssistantId { get; set; }
}

public class GetAutoTestAgentAndAssistantsResponse : SmartTalkResponse<GetAutoTestAgentAndAssistantsResponseData>
{
}

public class GetAutoTestAgentAndAssistantsResponseData
{
    public List<AgentDto> Agents { get; set; }
    
    public List<AiSpeechAssistantDto> Assistants { get; set; }
}