using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Services.RealtimeAiV2;
using SmartTalk.Core.Services.RealtimeAiV2.Adapters.Providers.OpenAi;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Settings.MiniMax;

public class MiniMaxTtsSettings : IConfigurationSetting
{
    public MiniMaxTtsSettings(IConfiguration configuration)
    {
        Enabled = configuration.GetValue<bool>("MiniMaxTts:Enabled", false);
        AssistantId = configuration.GetValue<string>("MiniMaxTts:AssistantId") ?? string.Empty;
        ServiceUrl = configuration.GetValue<string>("MiniMaxTts:ServiceUrl") ?? "wss://api.minimax.io/ws/v1/t2a_v2";
        ApiKey = configuration.GetValue<string>("MiniMaxTts:ApiKey") ?? string.Empty;
        Model = configuration.GetValue<string>("MiniMaxTts:Model") ?? "speech-2.8-turbo";
        DefaultVoiceId = configuration.GetValue<string>("MiniMaxTts:DefaultVoiceId") ?? "Chinese (Mandarin)_News_Anchor";
        SampleRate = configuration.GetValue<int>("MiniMaxTts:SampleRate", 8000);
        Speed = configuration.GetValue<double>("MiniMaxTts:Speed", 0.9);
        Volume = configuration.GetValue<double>("MiniMaxTts:Volume", 1.0);
        Pitch = configuration.GetValue<int>("MiniMaxTts:Pitch", 0);
        Bitrate = configuration.GetValue<int>("MiniMaxTts:Bitrate", 128000);
        SourceSampleRate = configuration.GetValue<int?>("MiniMaxTts:SourceSampleRate") ?? SampleRate;
    }

    public bool Enabled { get; set; }

    public string AssistantId { get; set; }

    public string ServiceUrl { get; set; }

    public string ApiKey { get; set; }

    public string Model { get; set; }

    public string DefaultVoiceId { get; set; }

    public int SampleRate { get; set; }

    public double Speed { get; set; }

    public double Volume { get; set; }

    public int Pitch { get; set; }

    public int Bitrate { get; set; }

    public int SourceSampleRate { get; set; }

    public bool IsEnabledForAssistant(int assistantId)
    {
        return Enabled &&
               !string.IsNullOrWhiteSpace(AssistantId) &&
               string.Equals(AssistantId.Trim(), assistantId.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public RealtimeAiTtsConfig BuildRealtimeAiTtsConfig(
        int assistantId,
        RealtimeAiProvider modelProvider,
        string modelVoice,
        int sampleRate,
        int? sourceSampleRate = null)
    {
        if (!IsEnabledForAssistant(assistantId)) return null;
        if (modelProvider != RealtimeAiProvider.OpenAi) return null;
        if (string.IsNullOrWhiteSpace(ApiKey)) return null;

        return new RealtimeAiTtsConfig
        {
            ProviderType = RealtimeAiTtsProviderType.MiniMax,
            ServiceUrl = ServiceUrl,
            ApiKey = ApiKey,
            Voice = ResolveMiniMaxVoiceId(modelVoice),
            TargetCodec = RealtimeAiAudioCodec.PCM16,
            SampleRate = sampleRate,
            ProviderSpecificConfig = new Dictionary<string, object>
            {
                ["model"] = Model,
                ["speed"] = Speed,
                ["vol"] = Volume,
                ["pitch"] = Pitch,
                ["bitrate"] = Bitrate,
                ["source_sample_rate"] = sourceSampleRate ?? SourceSampleRate
            }
        };
    }

    private string ResolveMiniMaxVoiceId(string modelVoice)
    {
        if (string.IsNullOrWhiteSpace(modelVoice)) return DefaultVoiceId;

        return OpenAiRealtimeAiProviderAdapter.SupportedVoices.Contains(modelVoice, StringComparer.OrdinalIgnoreCase)
            ? DefaultVoiceId
            : modelVoice;
    }
}
