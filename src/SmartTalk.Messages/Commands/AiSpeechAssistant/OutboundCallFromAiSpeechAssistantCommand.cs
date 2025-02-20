using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class OutboundCallFromAiSpeechAssistantCommand : ICommand
{
    public string Host { get; set; }
    
    public string From { get; set; }
    
    public string To { get; set; }
}

public class OutboundCallFromAiSpeechAssistantResponse : SmartTalkResponse;