using Mediator.Net.Contracts;
using SmartTalk.Messages.Enums.Audio;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.SpeechMatics;

public class AnalyzeAudioCommand : ICommand
{
    public byte[] AudioContent { get; set; }
    
    public string AudioUrl { get; set; }

    public AudioFileFormat AudioFileFormat { get; set; } = AudioFileFormat.Wav;
    
    public string SystemPrompt { get; set; }
    
    public string UserPrompt { get; set; }
}

public class AnalyzeAudioResponse : SmartTalkResponse<string>
{
}