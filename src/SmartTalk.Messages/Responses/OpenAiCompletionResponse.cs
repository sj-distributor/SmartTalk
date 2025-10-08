namespace SmartTalk.Messages.Responses;

public class OpenAiCompletionResponse
{
    public List<Choice> Choices { get; set; }
    public AudioOutput audio { get; set; }
    
    public class Choice
    {
        public MessageDto Message { get; set; }
        
        public class MessageDto
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }
    }
    
    public class AudioOutput
    {
        public string data { get; set; }
    }
}