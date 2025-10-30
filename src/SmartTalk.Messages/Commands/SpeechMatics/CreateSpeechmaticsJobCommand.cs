using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.SpeechMatics;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.SpeechMatics;

public class CreateSpeechmaticsJobCommand : ICommand
{
    public byte[] recordContent { get; set; }

    public string recordName { get; set; }

    public string language { get; set; }

    public SpeechMaticsJobScenario scenario { get; set; }
}

public class CreateSpeechmaticsJobResponse : SmartTalkResponse<string>
{
}