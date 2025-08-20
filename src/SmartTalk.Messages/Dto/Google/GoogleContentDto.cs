using SmartTalk.Messages.Attributes;

namespace SmartTalk.Messages.Dto.Google;

public class GoogleContentDto
{
    public string Role { get; set; }
    
    public List<GooglePartDto> Parts { get; set; }
}

public class GooglePartDto
{
    public bool Thought { get; set; }
    
    public string ThoughtSignature { get; set; }
    
    public string Text { get; set; }
    
    public GoogleBlobDto InlineData { get; set; }
}

public class GoogleBlobDto
{
    public string MimeType { get; set; }
    
    public string Data { get; set; }
}