using SmartTalk.Messages.Enums.AutoTest;

namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestScenarioDto
{
    public int Id { get; set; }

    public string KeyName { get; set; }
    
    public string Name { get; set; }
    
    public string InputSchema { get; set; }
    
    public string OutputSchema { get; set; }
    
    public string ActionConfig { get; set; }
    
    public AutoTestActionType ActionType { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    public DateTimeOffset? UpdatedAt { get; set; }
}