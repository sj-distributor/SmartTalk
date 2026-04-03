using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.AISpeechAssistant;

[Table("ai_speech_assistant_dynamic_config_relating_company")]
public class AiSpeechAssistantDynamicConfigRelatingCompany : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("config_id")]
    public int ConfigId { get; set; }
    
    [Column("company_id")]
    public int CompanyId { get; set; }
    
    [Column("company_name")]
    public string CompanyName { get; set; }
}