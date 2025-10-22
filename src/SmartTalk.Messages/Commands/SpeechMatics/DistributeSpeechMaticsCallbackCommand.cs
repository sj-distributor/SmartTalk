using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.SpeechMatics;

namespace SmartTalk.Messages.Commands.SpeechMatics;

public class DistributeSpeechMaticsCallbackCommand : ICommand
{
    public string CallBackMessage { get; set; }
}