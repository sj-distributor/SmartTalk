using Mediator.Net.Contracts;
using Microsoft.AspNetCore.Http;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class ConnectSpeechRealtimeCommand : ICommand
{
    public HttpContext HttpContext { get; set; }
}