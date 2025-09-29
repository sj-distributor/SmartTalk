namespace SmartTalk.Messages.Dto.HrInterView;

public class HrInterViewSettingQuestionDto
{
    public int Id { get; set; }
    
    public int SettingId { get; set; }
    
    public Guid SessionId { get; set; }
    
    public string Type { get; set; }
    
    public string Question { get; set; }
    
    public int Count { get; set; }

    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}