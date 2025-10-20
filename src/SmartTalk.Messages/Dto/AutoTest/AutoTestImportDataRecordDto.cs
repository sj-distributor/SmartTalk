using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestImportDataRecordDto
{
    public int Id { get; set; }
    
    public int ScenarioId { get; set; }
    
    public AutoTestImportDataRecordType Type { get; set; }

    public string OpConfig { get; set; }
    
    public AutoTestStatus Status { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset StartedAt { get; set; }
    
    public DateTimeOffset FinishedAt { get; set; }
}