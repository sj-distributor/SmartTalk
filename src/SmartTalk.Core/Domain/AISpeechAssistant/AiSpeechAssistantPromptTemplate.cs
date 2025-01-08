using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AIAssistant;

[Table("ai_speech_assistant_prompt_template")]
public class AiSpeechAssistantPromptTemplate : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("template")]
    public string Template { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedTime { get; set; }
}