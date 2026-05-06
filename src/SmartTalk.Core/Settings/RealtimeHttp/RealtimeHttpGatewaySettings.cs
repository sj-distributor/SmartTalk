using Microsoft.Extensions.Configuration;
using SmartTalk.Core.Settings;

namespace SmartTalk.Core.Settings.RealtimeHttp;

public class RealtimeHttpGatewaySettings : IConfigurationSetting
{
    public RealtimeHttpGatewaySettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("RealtimeHttpGateway");
        var ttsSection = section.GetSection("Tts");
        var scriptedSection = section.GetSection("ScriptedConversation");

        InternalWsBaseUrl = section.GetValue<string>("InternalWsBaseUrl") ?? string.Empty;
        DefaultResponseTimeoutMs = section.GetValue<int?>("DefaultResponseTimeoutMs") ?? 20000;
        RecentEventCapacity = section.GetValue<int?>("RecentEventCapacity") ?? 200;
        RecordingStorageBasePath = section.GetValue<string>("RecordingStorageBasePath") ?? string.Empty;
        RecordingProcessedFolder = section.GetValue<string>("RecordingProcessedFolder") ?? "processed";
        RecordingCallbackFolder = section.GetValue<string>("RecordingCallbackFolder") ?? "callbacks";

        Tts = new RealtimeHttpTtsSettings
        {
            Enabled = ttsSection.GetValue<bool?>("Enabled") ?? true,
            Model = ttsSection.GetValue<string>("Model") ?? "gpt-4o-mini-tts",
            Voice = ttsSection.GetValue<string>("Voice") ?? "alloy",
            ResponseFormat = ttsSection.GetValue<string>("ResponseFormat") ?? "pcm",
            ChunkDurationMs = ttsSection.GetValue<int?>("ChunkDurationMs") ?? 20,
            AppendSilenceMs = ttsSection.GetValue<int?>("AppendSilenceMs") ?? 250,
            SampleRate = ttsSection.GetValue<int?>("SampleRate") ?? 24000
        };

        var prompts = scriptedSection
            .GetSection("DefaultPrompts")
            .GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        ScriptedConversation = new RealtimeHttpScriptedConversationSettings
        {
            Enabled = scriptedSection.GetValue<bool?>("Enabled") ?? true,
            WarmupWaitTimeoutMs = scriptedSection.GetValue<int?>("WarmupWaitTimeoutMs") ?? 10000,
            MessageTimeoutMs = scriptedSection.GetValue<int?>("MessageTimeoutMs") ?? 40000,
            MaxExtraTurnWaitsWhenOutputEmpty = scriptedSection.GetValue<int?>("MaxExtraTurnWaitsWhenOutputEmpty") ?? 1,
            AutoDisconnectReason = scriptedSection.GetValue<string>("AutoDisconnectReason") ?? "scripted_two_turns_completed",
            DefaultPrompts = prompts.Count > 0
                ? prompts
                :
                [
                    "你好，请你先做一个简短自我介绍。",
                    "谢谢。请再用一句话总结你能如何帮助我。"
                ]
        };
    }

    public string InternalWsBaseUrl { get; set; } = string.Empty;

    public int DefaultResponseTimeoutMs { get; set; } = 20000;

    public int RecentEventCapacity { get; set; } = 200;

    public string RecordingStorageBasePath { get; set; } = string.Empty;

    public string RecordingProcessedFolder { get; set; } = "processed";

    public string RecordingCallbackFolder { get; set; } = "callbacks";

    public RealtimeHttpTtsSettings Tts { get; set; } = new();

    public RealtimeHttpScriptedConversationSettings ScriptedConversation { get; set; } = new();
}

public class RealtimeHttpTtsSettings
{
    public bool Enabled { get; set; } = true;

    public string Model { get; set; } = "gpt-4o-mini-tts";

    public string Voice { get; set; } = "alloy";

    public string ResponseFormat { get; set; } = "pcm";

    public int ChunkDurationMs { get; set; } = 20;

    public int AppendSilenceMs { get; set; } = 250;

    public int SampleRate { get; set; } = 24000;
}

public class RealtimeHttpScriptedConversationSettings
{
    public bool Enabled { get; set; } = true;

    public int WarmupWaitTimeoutMs { get; set; } = 10000;

    public int MessageTimeoutMs { get; set; } = 40000;

    public int MaxExtraTurnWaitsWhenOutputEmpty { get; set; } = 1;

    public string AutoDisconnectReason { get; set; } = "scripted_two_turns_completed";

    public List<string> DefaultPrompts { get; set; } =
    [
        "你好，请你先做一个简短自我介绍。",
        "谢谢。请再用一句话总结你能如何帮助我。"
    ];
}
