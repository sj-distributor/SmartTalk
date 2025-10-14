using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class ConfigureAiSpeechAssistantInboundRouteCommand : ICommand
{
    public string TargetNumber { get; set; }
    
    public string ForwardNUmber { get; set; }

    public bool Rollback { get; set; } = false;
}