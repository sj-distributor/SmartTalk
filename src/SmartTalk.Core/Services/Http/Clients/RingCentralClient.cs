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

        var nvc = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new("assertion", _ringCentralAuthenticationSettings.JwtAssertion)
        };

        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Basic {_ringCentralAuthenticationSettings.BasicAuth}" },
            { "Content-Type", "application/x-www-form-urlencoded" }
        };

        var response = await _httpClientFactory.PostAsync<RingCentralTokenResponseDto>(url, new FormUrlEncodedContent(nvc), headers: headers, cancellationToken: cancellationToken).ConfigureAwait(false);

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