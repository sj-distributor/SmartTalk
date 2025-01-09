namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantStreamContxtDto
{
    public string StreamSid { get; set; }

    public int LatestMediaTimestamp { get; set; } = 0;
        
    public string LastAssistantItem { get; set; }

    public Queue<string> MarkQueue { get; set; } = new Queue<string>();

    public long? ResponseStartTimestampTwilio { get; set; } = null;
        
    public bool InitialConversationSent { get; set; } = false;

    public bool ShowTimingMath { get; set; } = false;
}