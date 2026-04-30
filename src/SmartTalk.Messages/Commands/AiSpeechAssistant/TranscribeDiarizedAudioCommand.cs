using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class TranscribeDiarizedAudioCommand : ICommand
{
    public string RecordingUrl { get; set; }
}

public class TranscribeDiarizedAudioResponse : SmartTalkResponse<List<TranscribeDiarizedAudioSegmentDto>>;

public class TranscribeDiarizedAudioSegmentDto
{
    public double Start { get; set; }

    public double End { get; set; }

    public string Speaker { get; set; }

    public string Role { get; set; }

    public string Text { get; set; }
}
