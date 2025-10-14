using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.HrInterView;

namespace SmartTalk.Core.Domain.HrInterView;

[Table("hr_interview_session")]
public class HrInterViewSession : IEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Column("session_id")]
    public Guid SessionId { get; set; }
    
    [Column("message")] 
    public string Message { get; set; }
    
    [Column("file_url")]
    public string FileUrl { get; set; }
    
    [Column("question_type")]
    public HrInterViewSessionQuestionType QuestionType { get; set; }
    
    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}