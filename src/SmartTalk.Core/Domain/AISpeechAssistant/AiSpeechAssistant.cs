using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant")]
public class AiSpeechAssistant : IEntity<int>, IAgent, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("name"), StringLength(255)]
    public string Name { get; set; }

    [Column("language"), StringLength(255)]
    public string Language { get; set; }
    
    [Column("answering_number_id")]
    public int? AnsweringNumberId { get; set; }
    
    [Column("answering_number")]
    public string AnsweringNumber { get; set; }
    
    [Column("model_url")]
    public string ModelUrl { get; set; }
    
    [Column("model_name"), StringLength(255)]
    public string ModelName { get; set; }
    
    [Column("model_language"), StringLength(255)]
    public string ModelLanguage { get; set; }
    
    [Column("model_provider")]
    public RealtimeAiProvider ModelProvider { get; set; }
    
    [Column("model_voice")]
    public string ModelVoice { get; set; }

    // ── Realtime API GA session-config knobs (Phase 4.1 of Round 2 rollout) ───────
    // All NULLABLE. NULL means "use the same hard-coded default as today" — these
    // fields are only consumed in Phase 4.2+ when the matching env var is non-off.
    // Until then this is a pure schema add and the runtime path is unchanged.

    [Column("transcription_model"), StringLength(64)]
    public string TranscriptionModel { get; set; }

    [Column("transcription_language"), StringLength(8)]
    public string TranscriptionLanguage { get; set; }

    [Column("turn_detection_type"), StringLength(32)]
    public string TurnDetectionType { get; set; }

    [Column("turn_detection_threshold")]
    public decimal? TurnDetectionThreshold { get; set; }

    [Column("turn_detection_silence_ms")]
    public int? TurnDetectionSilenceMs { get; set; }

    [Column("input_noise_reduction_type"), StringLength(32)]
    public string InputNoiseReductionType { get; set; }

    [Column("max_response_output_tokens")]
    public int? MaxResponseOutputTokens { get; set; }

    [Column("output_audio_speed")]
    public decimal? OutputAudioSpeed { get; set; }

    // ─────────────────────────────────────────────────────────────────────────────

    [Column("agent_id")]
    public int AgentId { get; set; }
    
    [Column("custom_record_analyze_prompt")]
    public string CustomRecordAnalyzePrompt { get; set; }
    
    [Column("manual_record_whole_audio")]
    public bool ManualRecordWholeAudio { get; set; }
    
    [Column("custom_repeat_order_prompt")]
    public string CustomRepeatOrderPrompt { get; set; }
    
    [Column("channel"), StringLength(36)]
    public string Channel { get; set; }
    
    [Column("is_display")]
    public bool IsDisplay { get; set; }

    [Column("wait_interval")]
    public int WaitInterval { get; set; } = 500;
    
    [Column("is_transfer_human")]
    public bool IsTransferHuman { get; set; } = false;
    
    [Column("group_key")]
    public int GroupKey { get; set; }
    
    [Column("is_default")]
    public bool IsDefault { get; set; }
    
    [Column("is_allow_order_push")]
    public bool IsAllowOrderPush { get; set; }
    
    [Column("is_auto_generate_order")]
    public bool IsAutoGenerateOrder { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("created_by")]
    public int CreatedBy { get; set; }
    
    [NotMapped]
    public AiSpeechAssistantKnowledge Knowledge { get; set; }
    
    [NotMapped]
    public AiSpeechAssistantTimer Timer { get; set; }
}