using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_function_call")]
public class AiSpeechAssistantFunctionCall : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("name"), StringLength(255)]
    public string Name { get; set; }
    
    [Column("content")]
    public string Content { get; set; }
    
    [Column("type")]
    public AiSpeechAssistantSessionConfigType Type { get; set; }
    
    [Column("model_provider")]
    public RealtimeAiProvider ModelProvider { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}