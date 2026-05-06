using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Serilog;
using SmartTalk.Core.Ioc;
using SmartTalk.Core.Settings.OpenAi;
using SmartTalk.Core.Settings.RealtimeHttp;

namespace SmartTalk.Core.Services.RealtimeHttp;

public interface IRealtimeHttpTtsService : ISingletonDependency
{
    Task<byte[]> SynthesizePcm16Async(string text, CancellationToken cancellationToken);
}

public class RealtimeHttpTtsService : IRealtimeHttpTtsService
{
    private const string OpenAiSpeechEndpoint = "https://api.openai.com/v1/audio/speech";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiSettings _openAiSettings;
    private readonly RealtimeHttpGatewaySettings _settings;

    public RealtimeHttpTtsService(
        IHttpClientFactory httpClientFactory,
        OpenAiSettings openAiSettings,
        RealtimeHttpGatewaySettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _openAiSettings = openAiSettings;
        _settings = settings;
    }

    public async Task<byte[]> SynthesizePcm16Async(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var pcmBytes = await TrySynthesizeFromOpenAiAsync(text, cancellationToken).ConfigureAwait(false);

        if (pcmBytes.Length == 0)
        {
            Log.Warning("[RealtimeHttpGateway] OpenAI TTS unavailable, using local tone fallback.");
            pcmBytes = GenerateFallbackTonePcm(text);
        }

        return AppendSilence(pcmBytes, _settings.Tts.AppendSilenceMs, _settings.Tts.SampleRate);
    }

    private async Task<byte[]> TrySynthesizeFromOpenAiAsync(string text, CancellationToken cancellationToken)
    {
        if (!_settings.Tts.Enabled || string.IsNullOrWhiteSpace(_openAiSettings.ApiKey))
            return [];

        try
        {
            var requestPayload = new
            {
                model = _settings.Tts.Model,
                voice = _settings.Tts.Voice,
                input = text,
                response_format = _settings.Tts.ResponseFormat
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiSpeechEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiSettings.ApiKey);

            var client = _httpClientFactory.CreateClient(nameof(RealtimeHttpTtsService));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Log.Warning("[RealtimeHttpGateway] OpenAI TTS failed. StatusCode: {StatusCode}, Body: {Body}", response.StatusCode, body);
                return [];
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return bytes;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[RealtimeHttpGateway] OpenAI TTS request error.");
            return [];
        }
    }

    private byte[] GenerateFallbackTonePcm(string text)
    {
        var sampleRate = _settings.Tts.SampleRate;
        var toneUnitMs = 40;
        var durationMs = Math.Max(300, Math.Min(4000, text.Length * toneUnitMs));
        var totalSamples = sampleRate * durationMs / 1000;

        var pcm = new byte[totalSamples * 2];
        const double amplitude = 0.18;

        for (var i = 0; i < totalSamples; i++)
        {
            var t = i / (double)sampleRate;
            var ch = text[i % text.Length];
            var freq = 240 + (ch % 18) * 20;
            var sample = (short)(Math.Sin(2 * Math.PI * freq * t) * short.MaxValue * amplitude);

            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcm;
    }

    private static byte[] AppendSilence(byte[] pcmBytes, int appendSilenceMs, int sampleRate)
    {
        if (appendSilenceMs <= 0 || pcmBytes.Length == 0) return pcmBytes;

        var silenceBytes = sampleRate * appendSilenceMs / 1000 * 2;
        var result = new byte[pcmBytes.Length + silenceBytes];
        Buffer.BlockCopy(pcmBytes, 0, result, 0, pcmBytes.Length);
        return result;
    }
}
