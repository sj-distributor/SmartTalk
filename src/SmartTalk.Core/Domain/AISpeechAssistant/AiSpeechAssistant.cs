using System.ComponentModel.DataAnnotations;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant")]
public class AiSpeechAssistant : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("did_number"), StringLength(255)]
    public string Name { get; set; }
    
    [Column("did_number"), StringLength(32)]
    public string DidNumber { get; set; }
    
    [Column("scenario")]
    public AiSpeechAssistantScenario Scenario { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedTime { get; set; }
}