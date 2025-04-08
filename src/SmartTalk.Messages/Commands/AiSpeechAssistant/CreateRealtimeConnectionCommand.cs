using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class CreateRealtimeConnectionCommand : ICommand
{
    public string OfferSdp { get; set; }
    
    public int? AssistantId { get; set; }
    
    public string CustomPrompt { get; set; }
}

public class CreateRealtimeConnectionResponse : SmartTalkResponse<CreateRealtimeConnectionResponseData>;

public class CreateRealtimeConnectionResponseData
{
    public string AnswerSdp { get; set; }
    
    public object TurnDetection { get; set; }
}
