namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestDataSetItemDto
{
    public int Id { get; set; }
    
    public int DataSetId { get; set; }
    
    public int DataItemId { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}