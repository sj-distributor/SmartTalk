using SmartTalk.Messages.Enums.Google;

namespace SmartTalk.Messages.Dto.Google;

public class GoogleToolConfigDto
{
    public GoogleFunctionCallingConfigDto FunctionCallingConfig  { get; set; }
}

public class GoogleFunctionCallingConfigDto
{
    public GoogleToolModeType Mode { get; set; } = GoogleToolModeType.AUTO;
    
    public List<string> AllowedFunctionNames { get; set; }
}