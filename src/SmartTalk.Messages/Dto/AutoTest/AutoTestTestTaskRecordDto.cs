using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestTestTaskRecordDto
{
    public int Id { get; set; }
    
    public int TestTaskId { get; set; }
    
    public int ScenarioId { get; set; }
    
    public int DataSetId { get; set; }
    
    public int DataSetItemId { get; set; }
    
    public string InputSnapshot { get; set; }
    
    public string RequestJson { get; set; }
    
    public string RawOutput { get; set; }
    
    public string NormalizedOutput { get; set; }    
    
    public string EvaluationSummary { get; set; }
    
    public string ValidationErrors { get; set; }
    
    public AutoTestStatus Status { get; set; }
        
    public string ErrorText { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}