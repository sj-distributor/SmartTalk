namespace SmartTalk.Messages.Dto.HrInterView;

public class HrInterViewSettingDto
{
    public int Id { get; set; }
    
    public string Welcome { get; set; }
    
    public string EndMessage { get; set; }
    
    public Guid SessionId { get; set; }
    
    
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
}