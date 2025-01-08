using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class CallAiSpeechAssistantCommand : ICommand
{
    public string From { get; set; }
    
    public string To { get; set; }
}