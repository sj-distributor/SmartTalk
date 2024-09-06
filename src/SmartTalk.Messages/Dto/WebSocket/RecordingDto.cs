namespace SmartTalk.Messages.Dto.WebSocket;

public class RecordingDto
{
    public string name { get; set; }
    
    public string format { get; set; }
    
    public string state { get; set; }
    
    public string target_uri { get; set; }
    
    public int duration { get; set; }
    
    public int talking_duration { get; set; }
    
    public int silence_duration { get; set; }
}