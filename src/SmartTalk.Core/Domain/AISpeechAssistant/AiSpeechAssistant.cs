using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

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
    public AiSpeechAssistantProvider ModelProvider { get; set; }
    
    [Column("model_voice")]
    public string ModelVoice { get; set; }
    
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
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("created_by")]
    public int CreatedBy { get; set; }
    
    [NotMapped]
    public AiSpeechAssistantKnowledge Knowledge { get; set; }
}