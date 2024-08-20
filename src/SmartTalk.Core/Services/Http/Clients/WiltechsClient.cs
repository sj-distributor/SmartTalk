using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Authentication;
using SmartTalk.Messages.Dto.Wiltechs;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IWiltechsClient : IScopedDependency
{
    Task<WiltechsTokenResponseDto> TokenAsync(WiltechsTokenRequestDto request, CancellationToken cancellationToken);

    Task<WiltechsUserInfoDto> GetUserInfoAsync(string token, CancellationToken cancellationToken);
}

public class WiltechsClient : IWiltechsClient
{
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly WiltechsAuthenticationSettings _wiltechsAuthenticationSettings;
    
    public WiltechsClient(ISmartTalkHttpClientFactory httpClientFactory, WiltechsAuthenticationSettings wiltechsAuthenticationSettings)
    {
        _httpClientFactory = httpClientFactory;
        _wiltechsAuthenticationSettings = wiltechsAuthenticationSettings;
    }
    
    public async Task<WiltechsTokenResponseDto> TokenAsync(WiltechsTokenRequestDto request, CancellationToken cancellationToken)
    {
        var url = $"{_wiltechsAuthenticationSettings.BaseUrl}/token";
        
        var nvc = new List<KeyValuePair<string, string>>
        {
            new ("grant_type", "password"),
            new ("username", $"{request.Username}"),
            new ("password", $"{request.Password}")
        };

        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Basic NDUwYzZjMDNmYzQ0YzQzYjo3OWQ5MDJkYmZlM2Q3ODFm" }
        };

        var response = await _httpClientFactory.PostAsync<WiltechsTokenResponseDto>(
            url, new FormUrlEncodedContent(nvc), headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Wiltechs token response {@Response}", response);

        return response;
    }
    
    public async Task<WiltechsUserInfoDto> GetUserInfoAsync(string token, CancellationToken cancellationToken)
    {
        var url = _wiltechsAuthenticationSettings.Authority;
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"{token}" }
        };

        var user = await _httpClientFactory.GetAsync<WiltechsUserInfoDto>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);
        
        Log.Information("Wiltechs user info {@User}", user);

        if (user != null) user.AccessToken = token.Replace("Bearer ", "");
        
        return user;
    }
}