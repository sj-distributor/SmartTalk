using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.SpeechMatics;
using SmartTalk.Core.Settings.SpeechMatics;
using SmartTalk.Messages.Dto.SpeechMatics;
using SmartTalk.Messages.Enums.SpeechMatics;

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
    private readonly SpeechMaticsSettings _speechMaticsSetting;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly ISpeechMaticsDataProvider _speechMaticsDataProvider;
    
    public SpeechMaticsClient(SpeechMaticsSettings speechMaticsSetting, ISmartTalkHttpClientFactory httpClientFactory, ISpeechMaticsDataProvider speechMaticsDataProvider)
    {
        _speechMaticsSetting = speechMaticsSetting;
        _httpClientFactory = httpClientFactory;
        _speechMaticsDataProvider = speechMaticsDataProvider;
    }
    
    public async Task<string> CreateJobAsync(SpeechMaticsCreateJobRequestDto speechMaticsCreateJobRequestDto, SpeechMaticsCreateTranscriptionDto speechMaticsCreateTranscritionDto, CancellationToken cancellationToken)
    {
        var jobConfig = JsonConvert.SerializeObject(speechMaticsCreateJobRequestDto.JobConfig, Formatting.Indented);
        
        var headers = await GetHeadersAsync(cancellationToken).ConfigureAwait(false);
        
        var formData = new Dictionary<string, string>
        { 
            { "config", jobConfig }
        };
        var fileData = new Dictionary<string, (byte[], string)>
        {
            { "data_file", (speechMaticsCreateTranscritionDto.Data, speechMaticsCreateTranscritionDto.FileName) }
        };
        
        Log.Information("formData : {@formData} , fileData : {@fileData}", formData, fileData);
        
        return await _httpClientFactory.PostAsMultipartAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/", formData, fileData, cancellationToken, headers: headers, isNeedToReadErrorContent: true).ConfigureAwait(false);;
    }
    
    public async Task<SpeechMaticsGetAllJobsResponseDto> GetAllJobsAsync(CancellationToken cancellationToken)
    {
        var headers = await GetHeadersAsync(cancellationToken).ConfigureAwait(false);
        
        return await _httpClientFactory.GetAsync<SpeechMaticsGetAllJobsResponseDto>($"{_speechMaticsSetting.BaseUrl}/jobs", cancellationToken, headers: headers).ConfigureAwait(false);
    }

    public async Task<SpeechMaticsGetJobDetailResponseDto> GetJobDetailAsync(string jobId, CancellationToken cancellationToken)
    {
        var headers = await GetHeadersAsync(cancellationToken).ConfigureAwait(false);
        
        var data = await _httpClientFactory.GetAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/{jobId}", cancellationToken, headers: headers).ConfigureAwait(false);

        return JsonConvert.DeserializeObject<SpeechMaticsGetJobDetailResponseDto>(data);
    }
    
    public async Task<SpeechMaticsDeleteJobResponseDto> DeleteJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var headers = await GetHeadersAsync(cancellationToken).ConfigureAwait(false);
        
        var data = await _httpClientFactory.DeleteAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/{jobId}", cancellationToken, headers: headers).ConfigureAwait(false);

        return JsonConvert.DeserializeObject<SpeechMaticsDeleteJobResponseDto>(data);
    }

    public async Task<string> GetTranscriptAsync(string jobId, string format, CancellationToken cancellationToken)
    {
        var headers = await GetHeadersAsync(cancellationToken).ConfigureAwait(false);
        
        return await _httpClientFactory.GetAsync<string>($"{_speechMaticsSetting.BaseUrl}/jobs/{jobId}/transcript?format={format}", cancellationToken, headers: headers).ConfigureAwait(false);
    }

    public async Task<SpeechMaticsGetUsageResponseDto> GetUsageStatisticsAsync(SpeechMaticsGetUsageRequestDto speechMaticsGetUsageRequestDto, CancellationToken cancellationToken)
    {
        var headers = await GetHeadersAsync(cancellationToken).ConfigureAwait(false);
        
        return await _httpClientFactory.GetAsync<SpeechMaticsGetUsageResponseDto>($"{_speechMaticsSetting.BaseUrl}/usage?since={speechMaticsGetUsageRequestDto.Since}&until={speechMaticsGetUsageRequestDto.Until}", cancellationToken, headers: headers).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, string>> GetHeadersAsync(CancellationToken cancellationToken)
    {
        var speechMaticsKey = (await _speechMaticsDataProvider.GetSpeechMaticsKeysAsync([SpeechMaticsKeyStatus.Active], cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        
        Log.Information("SpeechMatics key is: {@speechMaticsKey}", speechMaticsKey?.Key);
        
        return new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {speechMaticsKey?.Key}" },
        };
    }
}