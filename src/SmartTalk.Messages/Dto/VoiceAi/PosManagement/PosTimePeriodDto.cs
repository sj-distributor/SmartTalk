namespace SmartTalk.Messages.Dto.VoiceAi.PosManagement;

public class PosTimePeriodDto
{
    public long Id { get; set; }
    
    public string Name { get; set; }
    
    public List<int> DayOfWeeks { get; set; }
    
    public string StartTime { get; set; }
    
    public string EndTime { get; set; }
}