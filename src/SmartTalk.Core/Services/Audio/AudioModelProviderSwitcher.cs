using SmartTalk.Core.Ioc;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio;

public interface IAudioModelProviderSwitcher : IScopedDependency
{
    IAudioModelProvider GetAudioModelProvider(AudioModelProviderType audioModelProviderType);
}

public class AudioModelProviderSwitcher : IAudioModelProviderSwitcher
{
    private readonly IEnumerable<IAudioModelProvider> _audioModelProviders;

    public AudioModelProviderSwitcher(IEnumerable<IAudioModelProvider> audioModelProviders)
    {
        _audioModelProviders = audioModelProviders;
    }
    
    public IAudioModelProvider GetAudioModelProvider(AudioModelProviderType audioModelProviderType)
    {
        return _audioModelProviders.FirstOrDefault(x => x.ModelProviderType == audioModelProviderType);
    }
}