namespace SmartTalk.Messages.Dto.HrInterView;

public class HrInterViewQuestionEventResponseDto
{
    public Guid SessionId { get; set; }
    
    public string EventType { get; set; }
    
    public byte[] Message { get; set; }
}