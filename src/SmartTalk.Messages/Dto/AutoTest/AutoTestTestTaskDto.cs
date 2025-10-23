using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestTestTaskDto
{
    public int Id { get; set; }
    
    public int ScenarioId { get; set; }
    
    public int DataSetId { get; set; }
    
    public string Params { get; set; }

    public AutoTestTestTaskStatus Status { get; set; } = AutoTestTestTaskStatus.Pending;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    public DateTimeOffset? StartedAt { get; set; }
    
    public DateTimeOffset? FinishedAt { get; set; }
    
    [NotMapped]
    public int TotalCount  { get; set; }
    
    [NotMapped]
    public int InProgressCount  { get; set; }
}