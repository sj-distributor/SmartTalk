namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class NumberPoolDto
{
    public int Id { get; set; }
    
    public string Number { get; set; }
    
    public bool IsUsed { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
}