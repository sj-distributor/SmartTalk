namespace SmartTalk.Messages.Dto.WebSocket;

public class PhoneOrderConversationDetailDto
{
    public string SessionId { get; set; }
    
    public string Question { get; set; }
    
    public string Answer { get; set; }

    public DateTimeOffset CreateDate { get; set; }
}