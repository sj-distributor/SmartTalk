using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AIAssistant;

[Table("ai_speech_assistant_user_profile")]
public class AiSpeechAssistantUserProfile : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("assistant_id")]
    public int AssistantId { get; set; }
    
    [Column("caller_number"), StringLength(32)]
    public string CallerNumber { get; set; }
    
    [Column("profile_json")]
    public string ProfileJson { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedTime { get; set; }
}