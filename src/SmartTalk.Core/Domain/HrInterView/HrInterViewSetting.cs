using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.HrInterView;

[Table("hr_interview_setting")]
public class HrInterViewSetting : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("welcome")] 
    public string Welcome { get; set; }
    
    [Column("end_message")]
    public string EndMessage { get; set; }

    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}