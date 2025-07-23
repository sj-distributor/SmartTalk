using Mediator.Net.Contracts;

namespace SmartTalk.Messages.Commands.SpeechMatics;

public class AudioToTextCommand : ICommand
{
    public byte[] AudioContent { get; set; }

    public string Prompt { get; set; }
}