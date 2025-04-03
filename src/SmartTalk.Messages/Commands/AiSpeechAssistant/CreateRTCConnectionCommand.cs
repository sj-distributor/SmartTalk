using Mediator.Net.Contracts;
using Smarties.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class CreateRTCConnectionCommand : ICommand
{
    public string OfferSdp { get; set; }
    
    public int? AssistantId { get; set; }
    
    public string CustomPrompt { get; set; }
}

public class CreateRTCConnectionResponse : SmartiesResponse<string>;
