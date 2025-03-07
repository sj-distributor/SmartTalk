using System.ComponentModel.DataAnnotations;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using System.ComponentModel.DataAnnotations.Schema;

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
    
    [Column("did_number"), StringLength(32)]
    public string DidNumber { get; set; }
    
    [Column("url"), StringLength(512)]
    public string Url { get; set; }
    
    [Column("voice"), StringLength(36)]
    public string Voice { get; set; }
    
    [Column("provider")]
    public AiSpeechAssistantProvider Provider { get; set; }
    
    [Column("agent_id")]
    public int AgentId { get; set; }
    
    [Column("greetings"), StringLength(1024)]
    public string Greetings { get; set; }
    
    [Column("custom_record_analyze_prompt")]
    public string CustomRecordAnalyzePrompt { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}