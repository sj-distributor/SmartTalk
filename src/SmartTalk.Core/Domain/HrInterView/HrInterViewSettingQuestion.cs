using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.HrInterView;

[Table("hr_interview_setting_question")]
public class HrInterViewSettingQuestion : IEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("setting_id")]
    public int SettingId { get; set; }
    
    [Column("session_id")]
    public Guid SessionId { get; set; }
    
    [Column("type")] 
    public string Type { get; set; }
    
    [Column("questions")] 
    public string Questions { get; set; }
    
    [Column("count")]
    public int Count { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}