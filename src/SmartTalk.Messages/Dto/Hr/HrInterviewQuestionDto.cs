using SmartTalk.Messages.Enums.Hr;

namespace SmartTalk.Messages.Dto.Hr;

public class HrInterviewQuestionDto
{
    public int Id { get; set; }
    
    public HrInterviewQuestionSection Section { get; set; }

    public string Question { get; set; }
    
    public bool IsUsing { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}