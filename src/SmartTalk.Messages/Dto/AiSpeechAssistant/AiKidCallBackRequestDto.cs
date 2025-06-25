namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiKidCallBackRequestDto
{
    public Guid Uuid { get; set; }
    
    public string Url { get; set; }
    
    public string SessionId { get; set; }
}