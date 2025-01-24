using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_human_contact")]
public class AiSpeechAssistantHumanContact : IEntity, IHasCreatedFields
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("human_phone"), StringLength(36)]
    public string HumanPhone { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}