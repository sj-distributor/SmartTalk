using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Requests.AiSpeechAssistant;

public class GetAiSpeechAssistantKnowledgeCapabilitiesRequest : IRequest
{
    public int StoreId { get; set; }

    public string Keyword { get; set; }
}

public class GetAiSpeechAssistantKnowledgeCapabilitiesResponse
    : SmartTalkResponse<GetAiSpeechAssistantKnowledgeCapabilitiesResponseData>
{
}

public class GetAiSpeechAssistantKnowledgeCapabilitiesResponseData
{
    public bool CanConfigure { get; set; }

    public List<AiSpeechAssistantKnowledgeCapabilityDto> Capabilities { get; set; } = [];
}
