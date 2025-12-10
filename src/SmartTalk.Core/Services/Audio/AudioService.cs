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
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly IAudioModelProviderSwitcher _audioModelProviderSwitcher;

    public AudioService(IAudioModelProviderSwitcher audioModelProviderSwitcher, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _audioModelProviderSwitcher = audioModelProviderSwitcher;
    }

    public async Task<AnalyzeAudioResponse> AnalyzeAudioAsync(AnalyzeAudioCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        var provider = _audioModelProviderSwitcher.GetAudioModelProvider(command.ModelProviderType);

        var audioData = await GetAudioBinaryDataAsync(command, cancellationToken).ConfigureAwait(false);

        var resultText = await provider.ExtractAudioDataFromModelProviderAsync(command, audioData, cancellationToken).ConfigureAwait(false);

        Log.Information("Audio analysis result: {Analysis}", resultText);

        return new AnalyzeAudioResponse
        {
            Data = resultText
        };
    }

    private async Task<BinaryData> GetAudioBinaryDataAsync(AnalyzeAudioCommand command, CancellationToken cancellationToken)
    {
        BinaryData audioData;

        if (!string.IsNullOrWhiteSpace(command.AudioUrl))
        {
            var httpClient = _httpClientFactory.CreateClient();

            await using var stream = await httpClient.GetStreamAsync(command.AudioUrl, cancellationToken).ConfigureAwait(false);

            audioData = await BinaryData.FromStreamAsync(stream, cancellationToken);
        }
        else
        {
            audioData = BinaryData.FromBytes(command.AudioContent);
        }
        
        return audioData;
    }

    public class AudioData
    {
        public AudioDataType Type { get; set; }
        
        public BinaryData BinaryContent { get; set; }
        
        public string Url { get; set; }
    }

    public enum AudioDataType
    {
        Binary,
        Url
    }
}