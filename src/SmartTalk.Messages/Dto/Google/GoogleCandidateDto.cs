using SmartTalk.Messages.Enums.Google;

namespace SmartTalk.Messages.Dto.Google;

public class GoogleCandidateDto
{
    public GoogleContentDto Content { get; set; }

    public GoogleFinishReasonType? FinishReason { get; set; }

    public List<GoogleSafetyRatingDto> SafetyRatings { get; set; }

    public GoogleCitationMetadataDto CitationMetadata { get; set; }

    public int? TokenCount { get; set; }

    public List<GoogleGroundingAttributionDto> GroundingAttributions { get; set; }

    public GoogleGroundingMetadataDto GroundingMetadata { get; set; }

    public double? AvgLogprobs { get; set; }

    public GoogleLogprobsResultDto LogprobsResult { get; set; }

    public GoogleUrlContextMetadataDto UrlContextMetadata { get; set; }

    public int? Index { get; set; }
}

public class GoogleSafetyRatingDto
{
    public GoogleHarmCategoryType Category { get; set; }
    
    public GoogleHarmProbabilityType Probability { get; set; }
    
    public bool? Blocked { get; set; }
}

public class GoogleCitationMetadataDto
{
    public List<GoogleCitationSourceDto> CitationSources { get; set; }
}

public class GoogleCitationSourceDto
{
    public int? StartIndex { get; set; }
    
    public int? EndIndex { get; set; }
    
    public string Uri { get; set; }
    
    public string License { get; set; }
}

public class GoogleGroundingAttributionDto
{
    public GoogleGroundingSourceIdDto SourceId { get; set; }
    
    public List<GoogleGroundingSegmentDto> GroundingSegment { get; set; }
    
    public double? ConfidenceScore { get; set; }
}

public class GoogleGroundingSourceIdDto
{
    public string WebResourceUri { get; set; }
    
    public GoogleGroundingPassageIdDto GroundingPassageId { get; set; }
}

public class GoogleGroundingPassageIdDto
{
    public string PassageId { get; set; }
    
    public int? PartIndex { get; set; }
}

public class GoogleGroundingSegmentDto
{
    public string Text { get; set; }
    
    public int? StartIndex { get; set; }
    
    public int? EndIndex { get; set; }
}

public class GoogleGroundingMetadataDto
{
    public List<GoogleGroundingSupportDto> GroundingSupport { get; set; }
    
    public List<GoogleGroundingChunkDto> GroundingChunks { get; set; }
    
    public List<string> SearchEntryPoint { get; set; }
    
    public double? GroundingScore { get; set; }
    
    public List<string> SupportBreakdown { get; set; }
}

public class GoogleGroundingSupportDto
{
    public GoogleGroundingSegmentDto Segment { get; set; }
    
    public List<GoogleGroundingAttributionDto> GroundingAttribution { get; set; }
    
    public List<string> SupportClaim { get; set; }
}

public class GoogleGroundingChunkDto
{
    public string ChunkId { get; set; }
    
    public string Content { get; set; }
    
    public List<GoogleGroundingSourceIdDto> SourceIds { get; set; }
}

public class GoogleLogprobsResultDto
{
    public List<GoogleLogprobDto> Logprobs { get; set; }
}

public class GoogleLogprobDto
{
    public string Token { get; set; }
    
    public double? Logprob { get; set; }
    
    public List<GoogleTopLogprobDto> TopLogprobs { get; set; }
}

public class GoogleTopLogprobDto
{
    public string Token { get; set; }
    
    public double? Logprob { get; set; }
}

public class GoogleUrlContextMetadataDto
{
    public List<GoogleUrlContextDto> UrlContexts { get; set; }
}

public class GoogleUrlContextDto
{
    public string Url { get; set; }
    
    public string Title { get; set; }
    
    public string Snippet { get; set; }
}