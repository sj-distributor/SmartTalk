namespace SmartTalk.Messages.Dto.HrInterView;

public class HrInterViewQuestionEventDto
{
    public Guid SessionId { get; set; }
    
    public string EventType { get; set; }
    
    public string Message { get; set; }
    
    public string MessageFileUrl { get; set; }
        
    public string EndMessage { get; set; }
    
    public string EndMessageFileUrl { get; set; }
}
