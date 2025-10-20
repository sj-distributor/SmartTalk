using SmartTalk.Messages.Enums.Google;

namespace SmartTalk.Messages.Dto.Google;

public class GoogleGenerateContentRequest
{
    public List<GoogleContentDto> Contents { get; set; }
    
    public List<GoogleToolDto> Tools { get; set; }
    
    public GoogleToolConfigDto ToolConfig { get; set; }
    
    public GoogleContentDto SystemInstruction { get; set; }
    
    public GoogleGenerationConfigDto GenerationConfig { get; set; }
    
    public string CachedContent { get; set; }
}

public class GoogleGenerateContentResponse
{
    public List<GoogleCandidateDto> Candidates { get; set; }
    
    public GooglePromptFeedbackDto PromptFeedback { get; set; }
    
    public object UsageMetadata { get; set; }
    
    public string ModelVersion { get; set; }
    
    public string ResponseId { get; set; }
}

public class GooglePromptFeedbackDto
{
    public GoogleBlockReasonType BlockReason { get; set; }
    
    public List<GoogleSafetyRatingDto> SafetyRatings { get; set; }
}

public class GoogleUsageMetadataDto
{
    public int? PromptTokenCount { get; set; }
    
    public int? CachedContentTokenCount { get; set; }
    
    public int? CandidatesTokenCount { get; set; }
    
    public int? ToolUsePromptTokenCount { get; set; }
    
    public int? ThoughtsTokenCount { get; set; }
    
    public int? TotalTokenCount { get; set; }
}
