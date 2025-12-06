using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio;

public interface IAudioModelProvider : IScopedDependency
{
    public AudioModelProviderType ModelProviderType { get; set; }
    
    Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, AudioService.AudioData audioData, CancellationToken cancellationToken);
}