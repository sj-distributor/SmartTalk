namespace SmartTalk.Messages.Dto.HrInterView;

public class AddHrInterViewQuestionDto
{
    public string QuestionType { get; set; }
    
    public List<string> Questions { get; set; }
    
    public int Count { get; set; }
}