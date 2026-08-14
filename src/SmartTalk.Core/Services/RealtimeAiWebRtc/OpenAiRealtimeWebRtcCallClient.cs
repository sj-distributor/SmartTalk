using System.Net.Http.Headers;
using System.Text;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Services.Http;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAiWebRtc;

public sealed class RealtimeAiWebRtcCallResult
{
    public string CallId { get; init; }

    public string AnswerSdp { get; init; }

    public Uri SidebandUri { get; init; }

    public Dictionary<string, string> SidebandHeaders { get; init; }
}

public interface IOpenAiRealtimeWebRtcCallClient : IScopedDependency
{
    Task<RealtimeAiWebRtcCallResult> CreateCallAsync(
        string offerSdp,
        string sessionJson,
        string providerServiceUrl,
        RealtimeAiServerRegion region,
        CancellationToken cancellationToken);
}

public sealed class OpenAiRealtimeWebRtcCallClient : IOpenAiRealtimeWebRtcCallClient
{
    private readonly OpenAiSettings _settings;
    private readonly ISmartTalkHttpClientFactory _httpClientFactory;

    public OpenAiRealtimeWebRtcCallClient(
        OpenAiSettings settings,
        ISmartTalkHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<RealtimeAiWebRtcCallResult> CreateCallAsync(
        string offerSdp,
        string sessionJson,
        string providerServiceUrl,
        RealtimeAiServerRegion region,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(offerSdp))
            throw new ArgumentException("SDP offer cannot be empty.", nameof(offerSdp));

        var (baseUrl, apiKey) = ResolveEndpoint(providerServiceUrl, region);
        var requestUri = new Uri($"{baseUrl.TrimEnd('/')}/v1/realtime/calls", UriKind.Absolute);

        using var multipart = new MultipartFormDataContent();
        using var sdpContent = new StringContent(offerSdp, Encoding.UTF8);
        using var sessionContent = new StringContent(sessionJson, Encoding.UTF8);

        sdpContent.Headers.ContentType = new MediaTypeHeaderValue("application/sdp");
        sessionContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        multipart.Add(sdpContent, "sdp");
        multipart.Add(sessionContent, "session");

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClientFactory.CreateClient(timeout: TimeSpan.FromSeconds(30))
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var answerSdp = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var boundedError = answerSdp.Length <= 2048 ? answerSdp : answerSdp[..2048];
            throw new InvalidOperationException(
                $"OpenAI WebRTC call creation failed ({(int)response.StatusCode}): {boundedError}");
        }

        var callId = ParseCallId(response.Headers.Location?.OriginalString);

        return new RealtimeAiWebRtcCallResult
        {
            CallId = callId,
            AnswerSdp = answerSdp,
            SidebandUri = BuildSidebandUri(baseUrl, callId),
            SidebandHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiKey}"
            }
        };
    }

    internal static string ParseCallId(string location)
    {
        var callId = location?.TrimEnd('/').Split('/').LastOrDefault();
        if (string.IsNullOrWhiteSpace(callId) || !callId.StartsWith("rtc_", StringComparison.Ordinal))
            throw new InvalidOperationException("OpenAI WebRTC response did not contain a valid call ID.");

        return callId;
    }

    internal static Uri BuildSidebandUri(string baseUrl, string callId)
    {
        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var builder = new UriBuilder(baseUri)
        {
            Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/v1/realtime",
            Query = $"call_id={Uri.EscapeDataString(callId)}"
        };

        return builder.Uri;
    }

    internal static string ResolveBaseUrl(string providerServiceUrl)
    {
        if (!Uri.TryCreate(providerServiceUrl, UriKind.Absolute, out var providerUri))
            throw new InvalidOperationException("The OpenAI realtime provider URL is not configured.");

        var builder = new UriBuilder(providerUri)
        {
            Scheme = providerUri.Scheme switch
            {
                "wss" => Uri.UriSchemeHttps,
                "ws" => Uri.UriSchemeHttp,
                "https" => Uri.UriSchemeHttps,
                "http" => Uri.UriSchemeHttp,
                _ => throw new InvalidOperationException($"Unsupported OpenAI realtime provider URL scheme: {providerUri.Scheme}.")
            },
            Query = string.Empty,
            Fragment = string.Empty
        };

        const string realtimePath = "/v1/realtime";
        var path = builder.Path.TrimEnd('/');
        builder.Path = path.EndsWith(realtimePath, StringComparison.OrdinalIgnoreCase)
            ? path[..^realtimePath.Length]
            : path;

        return builder.Uri.ToString().TrimEnd('/');
    }

    private (string BaseUrl, string ApiKey) ResolveEndpoint(
        string providerServiceUrl,
        RealtimeAiServerRegion region)
    {
        var apiKey = region == RealtimeAiServerRegion.US ? _settings.ApiKey : _settings.HkApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"OpenAI API key is not configured for region {region}.");

        return (ResolveBaseUrl(providerServiceUrl), apiKey);
    }

}
