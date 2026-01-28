using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordTaskDto
{
    public int StortId { get; set; }
    
    public int AgentId { get; set; }

    public int RecordId { get; set; }

    public DialogueScenarios? Scenarios { get; set; }
    
    public DateTimeOffset RecordDate { get; set; }

    public string TaskSource { get; set; }

    public string ProcessorName { get; set; }
}