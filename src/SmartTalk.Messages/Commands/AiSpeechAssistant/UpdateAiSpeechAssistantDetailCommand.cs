using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantDetailCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public string AssistantName { get; set; }
}

public class UpdateAiSpeechAssistantDetailResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}