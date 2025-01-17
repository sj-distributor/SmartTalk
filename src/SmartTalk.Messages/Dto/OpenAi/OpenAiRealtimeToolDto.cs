namespace SmartTalk.Messages.Dto.OpenAi;

public class OpenAiRealtimeToolDto
{
    public string Type { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    public OpenAiRealtimeToolParametersDto Parameters { get; set; }
}

public class OpenAiRealtimeToolParametersDto
{
    public string Type { get; set; }
    
    public object Properties { get; set; }
}