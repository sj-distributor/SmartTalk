using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Speechmatics;
using SmartTalk.Messages.Dto.Speechmatics;

namespace Smarties.Core.Services.Http.Clients;

public interface ISpeechmaticsClient : IScopedDependency
{
    Task<string> CreateJobAsync(SpeechmaticsCreateJobRequestDto speechmaticsCreateTranscriptionJobRequestDto, byte[] data, byte[] text, CancellationToken cancellationToken);
    
    Task<SpeechmaticsGetAllJobsResponseDto> GetAllJobsAsync(CancellationToken cancellationToken);
    
    Task<SpeechmaticsGetJobDetailResponseDto> GetJobDetailAsync(string jobId, CancellationToken cancellationToken);
    
    Task<SpeechmaticsDeleteJobResponseDto> DeleteJobAsync(string jobId, CancellationToken cancellationToken);
    
    Task<SpeechmaticsGetTranscriptionResponseDto> GetTranscriptAsync(string jobId, string format, CancellationToken cancellationToken);
    
    Task<string> GetAlignedTextAsync(string jobId, string tags, CancellationToken cancellationToken);
    
    Task<SpeechmaticsGetUsageResponseDto> GetUsageStatisticsAsync(SpeechmaticsGetUsageRequestDto speechmaticsGetUsageRequestDto, CancellationToken cancellationToken);
}

public class SpeechmaticsClient : ISpeechmaticsClient
{
    private readonly Dictionary<string, string> _headers;

    private readonly SpeechmaticsSettings _speechmaticsSetting;
    private readonly ISmartiesHttpClientFactory _httpClientFactory;
    
    public SpeechmaticsClient(SpeechmaticsSettings speechmaticsSetting, ISmartiesHttpClientFactory httpClientFactory)
    {
        _speechmaticsSetting = speechmaticsSetting;
        _httpClientFactory = httpClientFactory;

        _headers = new Dictionary<string, string>
        {
            { "Authorization",$"Bearer {speechmaticsSetting.ApiKey}" },
        };
    }
    
    public async Task<string> CreateJobAsync(SpeechmaticsCreateJobRequestDto speechmaticsCreateJobRequestDto, byte[] data,byte[] text, CancellationToken cancellationToken)
    {
        string jobConfig = JsonConvert.SerializeObject(speechmaticsCreateJobRequestDto.SpeechmaticsJobConfigDto, Formatting.Indented);
        
        var formData = new Dictionary<string, string>()
        {
            {"config",jobConfig}
        };
        var fileData = new Dictionary<string, (byte[], string)>
        {
            { "data_file", (data, "audio.wav") },
            { "text_file",(text,"config_text.txt")}
        };
        return await _httpClientFactory.PostAsMultipartAsync<string>($"{_speechmaticsSetting.BaseUrl}/jobs/",formData,fileData,cancellationToken,headers:_headers)
            .ConfigureAwait(false);
    }
    
    public async Task<SpeechmaticsGetAllJobsResponseDto> GetAllJobsAsync(CancellationToken cancellationToken)
    {
        return await _httpClientFactory
            .GetAsync<SpeechmaticsGetAllJobsResponseDto>($"{_speechmaticsSetting.BaseUrl}/jobs", cancellationToken,
                headers: _headers).ConfigureAwait(false);
    }

    public async Task<SpeechmaticsGetJobDetailResponseDto> GetJobDetailAsync(string jobId, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.GetAsync<SpeechmaticsGetJobDetailResponseDto>($"{_speechmaticsSetting.BaseUrl}/jobs/{jobId}",
            cancellationToken, headers: _headers).ConfigureAwait(false);
    }
    
    public async Task<SpeechmaticsDeleteJobResponseDto> DeleteJobAsync(string jobId, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.DeleteAsync<SpeechmaticsDeleteJobResponseDto>(
            $"{_speechmaticsSetting.BaseUrl}/jobs/{jobId}", cancellationToken, headers: _headers).ConfigureAwait(false);
    }

    public async Task<SpeechmaticsGetTranscriptionResponseDto> GetTranscriptAsync(string jobId, string format, CancellationToken cancellationToken)
    {
        return await _httpClientFactory
            .GetAsync<SpeechmaticsGetTranscriptionResponseDto>(
                $"{_speechmaticsSetting.BaseUrl}/jobs/{jobId}/transcript?format={format}", cancellationToken,
                headers: _headers).ConfigureAwait(false);
    }

    public async Task<string> GetAlignedTextAsync(string jobId, string tags, CancellationToken cancellationToken)
    {
        return await _httpClientFactory
            .GetAsync<string>($"{_speechmaticsSetting.BaseUrl}/jobs/{jobId}/alignment?tags={tags}", cancellationToken,
                headers: _headers).ConfigureAwait(false);
    }

    public async Task<SpeechmaticsGetUsageResponseDto> GetUsageStatisticsAsync(SpeechmaticsGetUsageRequestDto speechmaticsGetUsageRequestDto,
        CancellationToken cancellationToken)
    {
        return await _httpClientFactory.GetAsync<SpeechmaticsGetUsageResponseDto>(
            $"{_speechmaticsSetting.BaseUrl}/usage?since={speechmaticsGetUsageRequestDto.Since}&until={speechmaticsGetUsageRequestDto.Until}",
            cancellationToken, headers: _headers).ConfigureAwait(false);
    }
}