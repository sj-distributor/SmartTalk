using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_knowledge")]
public class AiSpeechAssistantKnowledge : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("json")]
    public string Json { get; set; }
    
    [Column("prompt")]
    public string Prompt { get; set; }
    
    [Column("version")]
    public string Version { get; set; }
    
    [Column("is_active", TypeName = "tinyint(1)")]
    public bool IsActive { get; set; }
    
    [Column("brief"), StringLength(128)]
    public string Brief { get; set; } = "未命名改動";
    
    [Column("greetings"), StringLength(1024)]
    public string Greetings { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
    
    [Column("created_by")]
    public int CreatedBy { get; set; }
    
    [NotMapped]
    public List<AiSpeechAssistantKnowledgeCopyRelated> KnowledgeCopyRelateds { get; set; }
}