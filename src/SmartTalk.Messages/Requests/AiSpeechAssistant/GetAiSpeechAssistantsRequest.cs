using Mediator.Net.Contracts;
using SmartTalk.Messages.Attributes;
using SmartTalk.Messages.Constants;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

[SmartTalkAuthorize(Permissions = new[] { SecurityStore.Permissions.CanViewKnowledge })]
public class GetAiSpeechAssistantsRequest : IRequest
{
    public int? PageIndex { get; set; }
    
    public int? PageSize { get; set; }
    
    public string Keyword { get; set; }
    
    public AiSpeechAssistantChannel? Channel { get; set; }
    
    public int? AgentId { get; set; }
}

public class GetAiSpeechAssistantsResponse : SmartTalkResponse<GetAiSpeechAssistantsResponseData>
{
}

public class GetAiSpeechAssistantsResponseData
{
    public int Count { get; set; }
    
    public List<AiSpeechAssistantDto> Assistants { get; set; }
}