using SmartTalk.Messages.Enums.Google;

namespace SmartTalk.Messages.Dto.Google;

public class GoogleToolDto
{
    public List<GoogleFunctionDeclarationDto> FunctionDeclarations { get; set; }

    public GoogleSearchRetrievalDto GoogleSearchRetrieval { get; set; }
    
    public object CodeExecution { get; set; }
    
    public GoogleSearchDto GoogleSearch { get; set; }
    
    public object UrlContext { get; set; }
}

public class GoogleSearchDto
{
    public GoogleIntervalDto TimeRangeFilter { get; set; }
}

public class GoogleFunctionDeclarationDto
{
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    public GoogleBehaviorType Behavior { get; set; }
    
    public GoogleSchemaDto Parameters { get; set; }
    
    public GoogleSchemaDto response { get; set; }
}

public class GoogleSearchRetrievalDto
{
    public GoogleDynamicRetrievalConfigDto DynamicRetrievalConfig { get; set; }
}

public class GoogleDynamicRetrievalConfigDto
{
    public GoogleRetrievalModeType Mode { get; set; }
    
    public int DynamicThreshold { get; set; }
}