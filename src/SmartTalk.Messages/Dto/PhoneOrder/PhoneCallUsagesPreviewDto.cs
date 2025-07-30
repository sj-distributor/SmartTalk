namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneCallUsagesPreviewDto
{
    public string Name { get; set; }
    
    public int AgentId { get; set; }
    
    public int ReportUsages { get; set; }
    
    public double TotalDuration { get; set; }
}