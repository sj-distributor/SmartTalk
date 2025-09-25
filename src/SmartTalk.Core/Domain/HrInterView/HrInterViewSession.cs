using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartTalk.Core.Domain.HrInterView;

[Table("hr_interview_session")]
public class HrInterViewSession : IEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("session_id")]
    public Guid SessionId { get; set; }
    
    [Column("message")] 
    public string Message { get; set; }
    
    [Column("bytes")]
    public string Bytes { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}