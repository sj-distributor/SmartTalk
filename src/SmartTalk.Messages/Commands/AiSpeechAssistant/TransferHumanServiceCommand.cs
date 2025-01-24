using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class TransferHumanServiceCommand : ICommand
{
    public string CallSid { get; set; }
    
    public string HumanPhone { get; set; }
}