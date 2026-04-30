using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class TranscribeAndDetectAudioLanguageCommand : ICommand
{
    public string RecordingUrl { get; set; }
}

public class TranscribeAndDetectAudioLanguageResponse : SmartTalkResponse<TranscribeAndDetectAudioLanguageResponseData>;

public class TranscribeAndDetectAudioLanguageResponseData
{
    public string Language { get; set; }

    public string Transcript { get; set; }

    public List<TranscribeAndDetectAudioLanguageRatioDto> Ratios { get; set; } = [];
}

public class TranscribeAndDetectAudioLanguageRatioDto
{
    public string Language { get; set; }

    public int Weight { get; set; }

    public double Ratio { get; set; }
}
