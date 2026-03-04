using System.ComponentModel.DataAnnotations;
using SmartTalk.Messages.Enums.SpeechMatics;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.SpeechMatics;

[Table("speech_matics_job")]
public class SpeechMaticsJob : IEntity
{
    public SpeechMaticsJob()
    {
        CreatedDate = DateTimeOffset.Now;
    }
    
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("job_id")]
    public string JobId { get; set; }
    
    [Column("scenario")]
    public SpeechMaticsJobScenario Scenario { get; set; }
    
    [Column("scenario_record_id")]
    public string ScenarioRecordId { get; set; }
    
    [Column("callback_url")]
    public string CallbackUrl { get; set; }
    
    [Column("callback_message")]
    public string CallbackMessage { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; }
}