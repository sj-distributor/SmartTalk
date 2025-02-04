using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class RecordAiSpeechAssistantCallCommand : ICommand
{
    public string Host { get; set; }
    
    public string CallSid { get; set; }
}