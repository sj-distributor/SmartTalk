using System.ComponentModel.DataAnnotations.Schema;
using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestTaskDto
{
    public int Id { get; set; }
    
    public int ScenarioId { get; set; }
    
    public int DataSetId { get; set; }
    
    public string Params { get; set; }

    public AutoTestTaskStatus Status { get; set; } = AutoTestTaskStatus.Pending;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    public DateTimeOffset? StartedAt { get; set; }
    
    public DateTimeOffset? FinishedAt { get; set; }
    
    [NotMapped]
    public int TotalCount  { get; set; }
    
    [NotMapped]
    public int InProgressCount  { get; set; }
    
    [NotMapped]
    public string DataSetName  { get; set; }
}