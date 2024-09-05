using Newtonsoft.Json;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.SpeechMatics;
using SmartTalk.Messages.Dto.SpeechMatics;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISpeechMaticsClient : IScopedDependency
{
    Task<string> CreateJobAsync(SpeechMaticsCreateJobRequestDto speechMaticsCreateJobRequestDto, SpeechMaticsCreateTranscriptionDto speechMaticsCreateTranscritionDto, CancellationToken cancellationToken);
    
    Task<SpeechMaticsGetAllJobsResponseDto> GetAllJobsAsync(CancellationToken cancellationToken);
    
    Task<SpeechMaticsGetJobDetailResponseDto> GetJobDetailAsync(string jobId, CancellationToken cancellationToken);
    
    Task<SpeechMaticsDeleteJobResponseDto> DeleteJobAsync(string jobId, CancellationToken cancellationToken);
    
    Task<string> GetTranscriptAsync(string jobId, string format, CancellationToken cancellationToken);
    
    Task<SpeechMaticsGetUsageResponseDto> GetUsageStatisticsAsync(SpeechMaticsGetUsageRequestDto speechMaticsGetUsageRequestDto, CancellationToken cancellationToken);
}

public class SpeechMaticsClient : ISpeechMaticsClient
{
    private readonly Dictionary<string, string> _headers;

    private readonly SpeechMaticsSettings _speechMaticsSetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    
    public SpeechMaticsClient(SpeechMaticsSettings speechMaticsSetting, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _speechMaticsSetting = speechMaticsSetting;
        _httpClientFactory = httpClientFactory;

        _headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {speechMaticsSetting.ApiKey}" },
        };
    }
    
    public async Task<string> CreateJobAsync(SpeechMaticsCreateJobRequestDto speechMaticsCreateJobRequestDto, SpeechMaticsCreateTranscriptionDto speechMaticsCreateTranscritionDto, CancellationToken cancellationToken)
    {
        var jobConfig = JsonConvert.SerializeObject(speechMaticsCreateJobRequestDto.JobConfig, Formatting.Indented);
        
        var formData = new Dictionary<string, string>()
        { 
            { "config", jobConfig }
        };
        var fileData = new Dictionary<string, (byte[], string)>
        {
            { "data_file", (speechMaticsCreateTranscritionDto.Data, speechMaticsCreateTranscritionDto.FileName) }
        };
        
        return await _httpClientFactory.PostAsMultipartAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/", formData, fileData, cancellationToken, headers: _headers).ConfigureAwait(false);
    }
    
    public async Task<SpeechMaticsGetAllJobsResponseDto> GetAllJobsAsync(CancellationToken cancellationToken)
    {
        return await _httpClientFactory.GetAsync<SpeechMaticsGetAllJobsResponseDto>($"{_speechMaticsSetting.BaseUrl}/jobs", cancellationToken, headers: _headers).ConfigureAwait(false);
    }

    public async Task<SpeechMaticsGetJobDetailResponseDto> GetJobDetailAsync(string jobId, CancellationToken cancellationToken)
    {
        var data = await _httpClientFactory.GetAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/{jobId}", cancellationToken, headers: _headers).ConfigureAwait(false);

        return JsonConvert.DeserializeObject<SpeechMaticsGetJobDetailResponseDto>(data);
    }
    
    public async Task<SpeechMaticsDeleteJobResponseDto> DeleteJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var data = await _httpClientFactory.DeleteAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/{jobId}", cancellationToken, headers: _headers).ConfigureAwait(false);

        return JsonConvert.DeserializeObject<SpeechMaticsDeleteJobResponseDto>(data);
    }

    public async Task<string> GetTranscriptAsync(string jobId, string format, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.GetAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/{jobId}/transcript?format={format}", cancellationToken, headers: _headers).ConfigureAwait(false);
    }

    public async Task<SpeechMaticsGetUsageResponseDto> GetUsageStatisticsAsync(SpeechMaticsGetUsageRequestDto speechMaticsGetUsageRequestDto, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.GetAsync<SpeechMaticsGetUsageResponseDto>($"{_speechMaticsSetting.BaseUrl}/usage?since={speechMaticsGetUsageRequestDto.Since}&until={speechMaticsGetUsageRequestDto.Until}", cancellationToken, headers: _headers).ConfigureAwait(false);
    }
}