using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Services.RealtimeAi.Wss;

public class RealtimeAiEngineContext
{
    public int AssistantId { get; set; }
    
    public string InitialPrompt { get; set; }

    public RealtimeAiAudioCodec InputFormat { get; set; } = RealtimeAiAudioCodec.PCM16;

    public RealtimeAiAudioCodec OutputFormat { get; set; } = RealtimeAiAudioCodec.PCM16;
}