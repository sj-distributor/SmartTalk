using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantDto
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public int AgentId { get; set; }
    
    public int AnsweringNumberId { get; set; }
    
    public string Language { get; set; }
    
    public string AnsweringNumber { get; set; }
    
    public string ModelUrl { get; set; }
    
    public RealtimeAiProvider ModelProvider { get; set; }
    
    public string ModelName { get; set; }

    public string ModelVoice { get; set; }

    public string ModelLanguage { get; set; }

    // ── Realtime API GA session-config knobs (Phase 4.2 of Round 2 rollout) ───────
    // All NULLABLE. NULL means "use today's hard-coded default". A non-null value
    // combined with `SQUID_SMARTTALK_REALTIME_ASSISTANT_CONFIG_ENFORCEMENT != off`
    // (read by the adapter) activates the per-assistant override.

    public string TranscriptionModel { get; set; }

    public string TranscriptionLanguage { get; set; }

    public string TurnDetectionType { get; set; }

    public decimal? TurnDetectionThreshold { get; set; }

    public int? TurnDetectionSilenceMs { get; set; }

    public string InputNoiseReductionType { get; set; }

    public int? MaxResponseOutputTokens { get; set; }

    public decimal? OutputAudioSpeed { get; set; }

    // ─────────────────────────────────────────────────────────────────────────────
    
    public string CustomRecordAnalyzePrompt { get; set; }
    
    public bool ManualRecordWholeAudio { get; set; }
    
    public string CustomRepeatOrderPrompt { get; set; }
    
    public string Channel { get; set; }
    
    public bool IsDisplay { get; set; }
    
    public int WaitInterval { get; set; }
    
    public bool IsTransferHuman { get; set; }
    
    public string TransferCallNumber { get; set; }
    
    public bool IsDefault { get; set; }
    
    public bool IsAllowOrderPush { get; set; }
    
    public bool IsAutoGenerateOrder { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
    
    public int CreatedBy { get; set; }
    
    public AiSpeechAssistantKnowledgeDto Knowledge { get; set; }

    public List<AiSpeechAssistantChannel> Channels => string.IsNullOrWhiteSpace(Channel)
        ? [] : Channel.Split(',').Select(x => Enum.TryParse(x, out AiSpeechAssistantChannel channel)
                ? channel : (AiSpeechAssistantChannel?)null).Where(x => x.HasValue).Select(x => x!.Value).ToList();

}