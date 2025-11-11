using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.Authentication;
using SmartTalk.Messages.Dto.RingCentral;

namespace SmartTalk.Core.Services.Http.Clients;

public interface IRingCentralClient : IScopedDependency
{
    Task<RingCentralTokenResponseDto> TokenAsync(CancellationToken cancellationToken);

    Task<RingCentralCallLogResponseDto> GetRingCentralRecordAsync(RingCentralCallLogRequestDto request, string token, CancellationToken cancellationToken);
}

public class RingCentralClient : IRingCentralClient
{
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;
    private readonly RingCentralAuthenticationSettings _ringCentralAuthenticationSettings;

    public RingCentralClient(ISmartTalkHttpClientFactory httpClientFactory, RingCentralAuthenticationSettings ringCentralAuthenticationSettings)
    {
        _httpClientFactory = httpClientFactory;
        _ringCentralAuthenticationSettings = ringCentralAuthenticationSettings;
    }

    public async Task<RingCentralTokenResponseDto> TokenAsync(CancellationToken cancellationToken)
    {
        var url = $"{_ringCentralAuthenticationSettings.BaseUrl}/restapi/oauth/token";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new KeyValuePair<string, string>("assertion", _ringCentralAuthenticationSettings.JwtAssertion)
        });
        
        var rawRequestBody = await content.ReadAsStringAsync();
        Log.Warning("RingCentral Token Request Body: {RawRequestBody}", rawRequestBody);

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Basic {_ringCentralAuthenticationSettings.BasicAuth}" }
        };
        
        Log.Information("RingCentral Token Request URL: {Url}", url);
        Log.Information("RingCentral Token Request Headers: {@Headers}", headers);

        var response = await _httpClientFactory.PostAsync<RingCentralTokenResponseDto>(url, content, headers: headers, cancellationToken: cancellationToken, isNeedToReadErrorContent: true).ConfigureAwait(false);

        Log.Information("RingCentral token response {@Response}", response);

        return response;
    }

    public async Task<RingCentralCallLogResponseDto> GetRingCentralRecordAsync(RingCentralCallLogRequestDto request, string token, CancellationToken cancellationToken)
    {
        var baseUrl = $"{_ringCentralAuthenticationSettings.BaseUrl}/restapi/v1.0/account/~/call-log";

        var query = new Dictionary<string, string>
        {
            { "phoneNumber", request.PhoneNumber },
            { "direction", request.Direction?.ToString().ToLower() },
            { "type", request.Type?.ToString().ToLower() },
            { "view", request.View?.ToString().ToLower() },
            { "withRecording", request.WithRecording?.ToString().ToLower() },
            { "dateFrom", request.DateFrom?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
            { "dateTo", request.DateTo?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
            { "page", request.Page?.ToString() },
            { "perPage", request.PerPage?.ToString() }
        };

        var queryString = string.Join("&", query.Where(kv => !string.IsNullOrEmpty(kv.Value)).Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));

        var url = string.IsNullOrEmpty(queryString) ? baseUrl : $"{baseUrl}?{queryString}";

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {token}" },
            { "Accept", "application/json" }
        };

        var response = await _httpClientFactory.GetAsync<RingCentralCallLogResponseDto>(url, headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

        Log.Information("RingCentral call log response {@Response}", response);

        return response;
    }
}