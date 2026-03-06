namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestDataSetDto
{
    public int Id { get; set; }
    
    public int ScenarioId { get; set; }

    public string KeyName { get; set; }
    
    public string Name { get; set; }

    public bool IsDelete { get; set; } = false;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    
    public int ImportRecordId { get; set; }
}