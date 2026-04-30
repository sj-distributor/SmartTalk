using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class DetectAudioLanguageCommand : ICommand
{
    public string RecordingUrl { get; set; }
}

public class DetectAudioLanguageResponse : SmartTalkResponse<string>;
