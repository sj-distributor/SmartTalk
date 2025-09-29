using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Ffmpeg;
using SmartTalk.Core.Settings.Speech;
using SmartTalk.Messages.Dto.WebSocket;
using SmartTalk.Messages.Enums.Speech;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISpeechClint : IScopedDependency
{
    Task<SpeechResponseDto> GetAudioFromTextAsync(TextToSpeechDto textToSpeech, CancellationToken cancellationToken);
}

public class SpeechClient : ISpeechClint
{
    private readonly IFfmpegService _ffmpegService;
    private readonly SpeechSettings _speechSettings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public SpeechClient(SpeechSettings speechSettings, ISmartTalkHttpClientFactory httpClientFactory, IFfmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
        _speechSettings = speechSettings;
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<SpeechResponseDto> GetAudioFromTextAsync(TextToSpeechDto textToSpeech, CancellationToken cancellationToken)
    {
        var header = ConstructSpeechClientHeader(SpeechServiceHeader.SugarTalk);

        Log.Information("Speech, text turn to voice :{textToSpeech}", JsonConvert.SerializeObject(textToSpeech));

        return await _httpClientFactory
            .PostAsJsonAsync<SpeechResponseDto>(
                $"{_speechSettings.SugarTalk.BaseUrl}/api/speech/tts", textToSpeech, cancellationToken, headers: header)
            .ConfigureAwait(false);
    }
    
    private Dictionary<string, string> ConstructSpeechClientHeader(SpeechServiceHeader serviceType)
    {
        var apiKey = serviceType switch
        {
            SpeechServiceHeader.EchoAvatar => _speechSettings.EchoAvatar.Apikey,
            SpeechServiceHeader.SugarTalk => _speechSettings.SugarTalk.Apikey,
            SpeechServiceHeader.Transcript => _speechSettings.Transcript.ApiKey,
            _ => throw new ArgumentException("Invalid service type")
        };

        return new Dictionary<string, string>
        {
            { "X-API-KEY", apiKey }
        };
    }
}