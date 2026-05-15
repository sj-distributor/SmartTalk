using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using SmartTalk.Core.Settings.Qwen;
using SmartTalk.Messages.Commands.SpeechMatics;
using SmartTalk.Messages.Enums.Audio;

namespace SmartTalk.Core.Services.Audio.Provider;

public class QwenAudioModelProvider : IAudioModelProvider
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly QwenSettings _qwenSettings;

    public QwenAudioModelProvider(QwenSettings qwenSettings, IHttpClientFactory httpClientFactory)
    {
        _qwenSettings = qwenSettings;
        _httpClientFactory = httpClientFactory;
    }

    public AudioModelProviderType ModelProviderType { get; set; } = AudioModelProviderType.Qwen;
    
    public async Task<string> ExtractAudioDataFromModelProviderAsync(AnalyzeAudioCommand command, BinaryData audioData, CancellationToken cancellationToken)
    {
        var messages = new List<object>();
        var userInput = new List<object>();
        
        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
        {
            messages.Add(new { role = "system", content = command.SystemPrompt });
        }
        
        userInput.Add(new
        {
            type = "audio_url",
            audio_url = new
            {
                url = command.AudioUrl
            }
        });
        
        if (!string.IsNullOrWhiteSpace(command.UserPrompt))
        {
            userInput.Add(new { type = "text", text = command.UserPrompt });
        }
        
        messages.Add(new { role = "user", content = userInput });
        
        var requestBody = new
        {
            stream = false,
            model = _qwenSettings.CrmModel,
            messages,
            modalities = new[] { "text" }
        };
        
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_qwenSettings.CrmApiKey}" }
        };
        var requestUrl = $"{_qwenSettings.CrmBaseUrl}/chat/completions";
        
        Log.Information("LLM http call url: {CallUrl} ,headers: {CallHeader}, request body: {@requestBody}", requestUrl, ToMaskedHeadersForLog(headers), requestBody);
        
        Log.Information("Qwen LLM call start time: {Now}", DateTimeOffset.Now);
        
        var response = new QwenChatCompletionResponse();
        var requestBodyJson = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = RequestTimeout;

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _qwenSettings.CrmApiKey);

            using var httpResponse = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                Log.Error(
                    "Qwen LLM http error. StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}, Response: {Response}",
                    (int)httpResponse.StatusCode,
                    httpResponse.ReasonPhrase,
                    responseContent);
            }
            else if (!string.IsNullOrWhiteSpace(responseContent))
            {
                response = JsonConvert.DeserializeObject<QwenChatCompletionResponse>(responseContent) ?? new QwenChatCompletionResponse();
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Qwen LLM calling exception");
        }
        
        Log.Information("Qwen LLM call end time: {Now}", DateTimeOffset.Now);
        
        return response?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }
    
    private IReadOnlyDictionary<string, string> ToMaskedHeadersForLog(IReadOnlyDictionary<string, string> headers)
    {
        if (headers == null)
            return new Dictionary<string, string>();

        return headers.ToDictionary(p => p.Key, p => MaskHeaderValue(p.Value));
    }
    
    private string MaskHeaderValue(string value)
    {
        const int visiblePrefixLength = 8;
        const int visibleSuffixLength = 4;
        const int minLengthToMask = visiblePrefixLength + visibleSuffixLength;

        if (string.IsNullOrEmpty(value) || value.Length <= minLengthToMask)
        {
            return value;
        }

        var middleMask = new string('*', value.Length - minLengthToMask);

        return $"{value[..visiblePrefixLength]}{middleMask}{value[^visibleSuffixLength..]}";
    }

    private class QwenChatCompletionResponse
    {
        public List<QwenChoice> Choices { get; set; }
    }

    private class QwenChoice
    {
        public QwenMessage Message { get; set; }
    }

    private class QwenMessage
    {
        public string Content { get; set; }
    }
}
