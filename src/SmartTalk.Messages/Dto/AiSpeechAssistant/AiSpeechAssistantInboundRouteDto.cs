namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantInboundRouteDto
{
    public int Id { get; set; }

    public string From { get; set; }

    public string To { get; set; }
    
    public string TimeZone { get; set; }
    
    public TimeSpan? StartTime { get; set; }
    
    public TimeSpan? EndTime { get; set; }
    
    public bool IsFullDay { get; set; }
    
    public string DayOfWeek { get; set; }

    public int? ForwardAssistantId { get; set; }
    
    public string ForwardNumber { get; set; }
    
    public int Priority { get; set; }
    
    public bool IsFallback { get; set; }
    
    public string Remarks { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}