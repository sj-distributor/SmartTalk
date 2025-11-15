namespace SmartTalk.Messages.Dto.AutoTest;

public class AutoTestDataItemDto
{
    public int Id { get; set; }

    public int ScenarioId { get; set; }

    public int ImportRecordId { get; set; }

    public string InputJson { get; set; }

    public string Quality { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public AutoTestImportDataRecordDto ImportRecord { get; set; }
}