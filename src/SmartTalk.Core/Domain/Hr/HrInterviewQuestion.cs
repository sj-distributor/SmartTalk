using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.Hr;

namespace SmartTalk.Core.Domain.Hr;

[Table("hr_interview_question")]
public class HrInterviewQuestion : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("section")]
    public HrInterviewQuestionSection Section { get; set; }

    [Column("question")]
    public string Question { get; set; }
    
    [Column("is_using")]
    public bool IsUsing { get; set; }

    [Column("created_date")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}