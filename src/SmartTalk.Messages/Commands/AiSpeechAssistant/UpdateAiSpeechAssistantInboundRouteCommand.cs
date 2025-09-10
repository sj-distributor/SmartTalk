using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantInboundRouteCommand : ICommand
{
    public int RouteId { get; set; }
    
    public string Number { get; set; }
    
    public string Remarks { get; set; }
}

public class UpdateAiSpeechAssistantInboundRouteResponse : SmartTalkResponse<AiSpeechAssistantInboundRouteDto>;