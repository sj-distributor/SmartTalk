using System.Net.Http.Headers;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using SmartTalk.Core.Ioc;
using Smarties.Messages.Requests.Ask;
using SmartTalk.Core.Settings.Smarties;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Dto.Smarties;

namespace SmartTalk.Core.Services.Http.Clients;

public interface ISmartiesClient : IScopedDependency
{
    Task<AskGptResponse> PerformQueryAsync(AskGptRequest request, CancellationToken cancellationToken);
    
    Task<AskGptEmbeddingResponseDto> GetEmbeddingAsync(AskGptEmbeddingRequestDto request, CancellationToken cancellationToken);

    Task CallBackSmartiesAiSpeechAssistantRecordAsync(AiSpeechAssistantCallBackRequestDto request, CancellationToken cancellationToken);
    
    Task CallBackSmartiesAiKidRecordAsync(AiKidCallBackRequestDto request, CancellationToken cancellationToken);
    
    Task<GetCrmCustomerInfoResponseDto> GetCrmCustomerInfoAsync(Guid kidUUid, CancellationToken cancellationToken);
    
    Task CallBackSmartiesAiKidConversationsAsync(AiKidConversationCallBackRequestDto request, CancellationToken cancellationToken);

    Task<GetSaleAutoCallNumberResponse> GetSaleAutoCallNumberAsync(GetSaleAutoCallNumberRequest request, CancellationToken cancellationToken);

    Task<UploadAttachmentResponse> UploadFileAsync(byte[] fileBytes, CancellationToken cancellationToken);
}

public class SmartiesClient : ISmartiesClient
{
    private readonly SmartiesSetting _smartiesSettings;
    private readonly Dictionary<string, string> _headers;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    
    public SmartiesClient(SmartiesSetting smartiesSettings, ISmartTalkHttpClientFactory httpClientFactory)
    {
        _smartiesSettings = smartiesSettings;
        _httpClientFactory = httpClientFactory;
        
        _headers = new Dictionary<string, string>
        {
            { "X-API-KEY", _smartiesSettings.ApiKey }
        };
    }
    
    public async Task<AskGptResponse> PerformQueryAsync(AskGptRequest request, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.PostAsJsonAsync<AskGptResponse>(
            $"{_smartiesSettings.BaseUrl}/api/Ask/general/query", request, cancellationToken, headers: _headers).ConfigureAwait(false);
    }
    
    public async Task<AskGptEmbeddingResponseDto> GetEmbeddingAsync(AskGptEmbeddingRequestDto request, CancellationToken cancellationToken)
    {
        Log.Information("Ask gpt embedding request: {@Request}", request);

        var response = await _httpClientFactory.PostAsJsonAsync<AskGptEmbeddingResponseDto>(_smartiesSettings.BaseUrl + "/api/Ask/embedding", request, cancellationToken, headers: _headers).ConfigureAwait(false);
        
        Log.Information("Ask gpt embedding response: {@Response}", response);

        return response;
    }

    public async Task CallBackSmartiesAiSpeechAssistantRecordAsync(AiSpeechAssistantCallBackRequestDto request, CancellationToken cancellationToken)
    {
        await _httpClientFactory.PostAsJsonAsync(_smartiesSettings.AiSpeechAssistantCallBackUrl, request, cancellationToken, headers: _headers).ConfigureAwait(false);
    }

    public async Task CallBackSmartiesAiKidRecordAsync(AiKidCallBackRequestDto request, CancellationToken cancellationToken)
    {
        await _httpClientFactory.PostAsJsonAsync($"{_smartiesSettings.BaseUrl}/api/AiKid/record/callback", request, cancellationToken, headers: _headers).ConfigureAwait(false);
    }

    public async Task<GetCrmCustomerInfoResponseDto> GetCrmCustomerInfoAsync(Guid kidUUid, CancellationToken cancellationToken)
    {
        return await _httpClientFactory.GetAsync<GetCrmCustomerInfoResponseDto>($"{_smartiesSettings.BaseUrl}/api/Crm/info/{kidUUid}", cancellationToken, headers: _headers).ConfigureAwait(false);
    }
    
    public async Task CallBackSmartiesAiKidConversationsAsync(AiKidConversationCallBackRequestDto request, CancellationToken cancellationToken)
    {
        await _httpClientFactory.PostAsJsonAsync($"{_smartiesSettings.BaseUrl}/api/AiKid/conversation/callback", request, cancellationToken, headers: _headers).ConfigureAwait(false);
    }
    
    public async Task<GetSaleAutoCallNumberResponse> GetSaleAutoCallNumberAsync(GetSaleAutoCallNumberRequest request, CancellationToken cancellationToken)
    {
        Log.Information("GetSaleAutoCallNumber request: {@Request}", request);

        var response = await _httpClientFactory.GetAsync<GetSaleAutoCallNumberResponse>($"{_smartiesSettings.BaseUrl}/api/AutoCall/number?Id={request.Id}", cancellationToken, headers: _headers).ConfigureAwait(false);
        
        Log.Information("GetSaleAutoCallNumber response: {@Response}", response);

        return response;
    }
    
    public async Task<UploadAttachmentResponse> UploadFileAsync(byte[] fileBytes, CancellationToken cancellationToken)
    {
        var form = new MultipartFormDataContent();

        using var memoryStream = new MemoryStream(fileBytes);
        
        var fileContent = new StreamContent(memoryStream);
        
        var fileName = $"hr-inter-view{Guid.NewGuid()}.wav";
        
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(GetMimeTypeFromExtension(fileName));

        form.Add(fileContent, "file", fileName);
        
        var response = await _httpClientFactory.PostAsync<UploadAttachmentResponse>(_smartiesSettings.BaseUrl + "/api/Attachment/upload", form, cancellationToken, headers: _headers).ConfigureAwait(false);
        
        Log.Information("UploadFileAsync response: {@Response}", response);
        
        return response;
        
    }
    
    public static string GetMimeTypeFromExtension(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(fileName, out var contentType) ? contentType : "application/octet-stream";
    }
}