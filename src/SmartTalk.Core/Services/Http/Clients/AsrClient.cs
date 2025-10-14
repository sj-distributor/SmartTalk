using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Asr;
using SmartTalk.Messages.Dto.Asr;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IAsrClient : IScopedDependency
{
    Task<AsrTranscriptionResponseDto> TranscriptionAsync(AsrTranscriptionDto transcription, CancellationToken cancellationToken);
}

public class AsrClient : IAsrClient
{
    private readonly AsrSettings _asrSettings;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;

    public AsrClient(ISmartiesHttpClientFactory httpClientFactory, AsrSettings asrSettings)
    {
        _asrSettings = asrSettings;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AsrTranscriptionResponseDto> TranscriptionAsync(AsrTranscriptionDto transcription, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", _asrSettings.Authorization }
        };
        
        var formData = new (string Key, string Value)[]
            {
                ("language", transcription.Language),
                ("response_format", transcription.ResponseFormat)
            }.Where(x => !string.IsNullOrEmpty(x.Value)).ToDictionary(x => x.Key, x => x.Value);
        
        var fileData = new Dictionary<string, (byte[], string)>
        {
            { "file", (transcription.File, string.IsNullOrEmpty(transcription.FileName) ? "file.wav" : transcription.FileName) }
        };
        
        var response = await _httpClientFactory.PostAsMultipartAsync<AsrTranscriptionResponseDto>(
            $"{_asrSettings.BaseUrl}/v1/audio/transcriptions", formData, fileData, headers: headers, timeout: TimeSpan.FromMinutes(10), cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("Audio Transcription Response: {@Response}", response);
        
        return response;
    }
}