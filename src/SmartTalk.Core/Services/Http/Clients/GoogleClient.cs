using System.Text;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Google;
using SmartTalk.Messages.Dto.Google;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IGoogleClient : IScopedDependency
{
    Task<GoogleGenerateContentResponse> GenerateContentAsync(GoogleGenerateContentRequest request, string model, CancellationToken cancellationToken);
}

public class GoogleClient : IGoogleClient
{
    private readonly GoogleSettings _googleSettings;
    private readonly ISmartTalkHttpClientFactory _httpClient;

    public GoogleClient(ISmartTalkHttpClientFactory httpClient, GoogleSettings googleSettings)
    {
        _httpClient = httpClient;
        _googleSettings = googleSettings;
    }

    public async Task<GoogleGenerateContentResponse> GenerateContentAsync(GoogleGenerateContentRequest request, string model, CancellationToken cancellationToken)
    {
        var requestBody = JsonConvert.SerializeObject(request, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        });
        
        Log.Information("Sending request to Google API: {RequestBody}", requestBody);
        
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync<GoogleGenerateContentResponse>(
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_googleSettings.ApiKey}", content, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Google Generate Content Response: {Response}", response);
        
        return response;
    }
}