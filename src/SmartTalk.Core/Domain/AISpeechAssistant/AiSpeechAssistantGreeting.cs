using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AIKnowledgeBase;

[Table("ai_speech_assistant_greeting")]
public class AiSpeechAssistantGreeting : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("text")]
    public string Text { get; set; }
    
    [Column("version")]
    public string Version { get; set; }
    
    [Column("is_active", TypeName = "tinyint(1)")]
    public bool IsActive { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; }
}