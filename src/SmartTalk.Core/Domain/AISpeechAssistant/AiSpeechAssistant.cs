using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant")]
public class AiSpeechAssistant : IEntity, IHasCreatedFields
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
    
    [Column("model_provider")]
    public AiSpeechAssistantProvider ModelProvider { get; set; }
    
    [Column("model_voice")]
    public string ModelVoice { get; set; }
    
    [Column("agent_id")]
    public int AgentId { get; set; }
    
    [Column("custom_record_analyze_prompt")]
    public string CustomRecordAnalyzePrompt { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("created_by")]
    public int CreatedBy { get; set; }
}