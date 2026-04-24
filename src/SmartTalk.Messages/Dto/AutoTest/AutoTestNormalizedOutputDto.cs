namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestNormalizedOutputDto
{
    public bool IsMatched { get; set; }
    
    public string Recording { get; set; }
    
    public List<AutoTestOrderItemDto> AiOrder { get; set; }
    
    public List<AutoTestInputDetail> ActualOrder { get; set; }
    
    public string Report { get; set; }
}