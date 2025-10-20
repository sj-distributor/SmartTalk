using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestTestTaskDto
{
    public int Id { get; set; }
    
    public int ScenarioId { get; set; }
    
    public int DataSetId { get; set; }
    
    public string Params { get; set; }
    
    public AutoTestStatus Status { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    public DateTimeOffset StartedAt { get; set; }
    
    public DateTimeOffset FinishedAt { get; set; }
}