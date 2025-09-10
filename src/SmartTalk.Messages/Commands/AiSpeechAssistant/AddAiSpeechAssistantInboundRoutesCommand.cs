using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantInboundRoutesCommand : ICommand
{
    public int AssistantId { get; set; }
    
    public string TargetNumber { get; set; }
    
    public List<AiSpeechAssistantNumberWhitelistDto> Numbers { get; set; }
}

public class AddAiSpeechAssistantInboundRoutesResponse : SmartTalkResponse<List<AiSpeechAssistantInboundRouteDto>>;