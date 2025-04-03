using Mediator.Net.Contracts;
using Smarties.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class CreateRealtimeConnectionCommand : ICommand
{
    public string OfferSdp { get; set; }
    
    public int? AssistantId { get; set; }
    
    public string CustomPrompt { get; set; }
}

public class CreateRealtimeConnectionResponse : SmartiesResponse<string>;
