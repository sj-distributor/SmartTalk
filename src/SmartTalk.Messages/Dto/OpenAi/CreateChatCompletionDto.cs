using SmartTalk.Messages.Enums.OpenAi;

namespace SmartTalk.Messages.Dto.OpenAi;

public class ChatCompletionMessageDto
{
    public ChatCompletionMessageRole Role { get; set; }
    
    public string Content { get; set; }
}

public class ChatCompletionResponseDto
{
    public ChatCompletionResponseDto()
    {
        Choices = new List<CompletionChoiceDto>();
    }
    
    public string Id { get; set; }
    
    public int CreatedAt { get; set; }
    
    public DateTime CreatedDate => DateTimeOffset.FromUnixTimeSeconds(CreatedAt).DateTime;

    public string Model { get; set; }

    public List<CompletionChoiceDto> Choices { get; set; }
    
    public CompletionUsageDto Usage { get; set; }
    
    public string Response
    {
        get
        {
            if (Choices == null || !Choices.Any())
                return string.Empty;

            return !string.IsNullOrEmpty(Choices.First().Message?.Content)
                ? Choices.First().Message?.Content
                : string.Empty;
        }
    }
}

public class CompletionChoiceDto
{
    public int? Index { get; set; }
    
    public string FinishReason { get; set; }

    public CompletionChoiceMessageDto Message { get; set; }
}

public class CompletionChoiceMessageDto
{
    public string Role { get; set; }
    
    public string Content { get; set; }
}

public class CompletionUsageDto
{
    public int PromptTokens { get; set; }
    
    public int? CompletionTokens { get; set; }
    
    public int TotalTokens { get; set; }
}