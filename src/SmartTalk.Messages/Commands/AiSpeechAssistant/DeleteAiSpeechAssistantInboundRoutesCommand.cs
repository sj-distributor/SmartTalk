using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class DeleteAiSpeechAssistantInboundRoutesCommand : ICommand
{
    public List<int> RouteIds { get; set; }
}

public class DeleteAiSpeechAssistantInboundRoutesResponse : SmartTalkResponse<List<AiSpeechAssistantInboundRouteDto>>;