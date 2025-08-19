using OpenAI.Chat;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio;

public interface IAudioService : IScopedDependency
{
    Task<AnalyzeAudioResponse> AnalyzeAudioAsync(AnalyzeAudioCommand command, CancellationToken cancellationToken);
}

public class AudioService : IAudioService
{
    private readonly IAudioModelProviderSwitcher _audioModelProviderSwitcher;

    public AudioService(IAudioModelProviderSwitcher audioModelProviderSwitcher)
    {
        _audioModelProviderSwitcher = audioModelProviderSwitcher;
    }

    public async Task<AnalyzeAudioResponse> AnalyzeAudioAsync(AnalyzeAudioCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        var provider = _audioModelProviderSwitcher.GetAudioModelProvider(command.ModelProviderType);

        var resultText = await provider.ExtractAudioDataFromModelProviderAsync(command, cancellationToken);

        Log.Information("Audio analysis result: {Analysis}", resultText);

        return new AnalyzeAudioResponse
        {
            Data = resultText
        };
    }
}