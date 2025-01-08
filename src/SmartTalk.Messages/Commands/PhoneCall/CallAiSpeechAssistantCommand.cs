using Twilio.AspNet.Core;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.PhoneCall;

public class CallAiSpeechAssistantCommand : ICommand
{
    public string Host { get; set; }
    
    public string From { get; set; }
    
    public string To { get; set; }
}

public class CallAiSpeechAssistantResponse : SmartTalkResponse<TwiMLResult>;