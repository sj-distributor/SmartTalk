using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Requests.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand : ICommand
{
    public int StoreId { get; set; }

    public List<UpdateAiSpeechAssistantKnowledgeCapabilityDto> Items { get; set; } = [];
}

public class UpdateAiSpeechAssistantKnowledgeCapabilitiesResponse
    : SmartTalkResponse<GetAiSpeechAssistantKnowledgeCapabilitiesResponseData>
{
}
