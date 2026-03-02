using SmartTalk.Messages.Enums.PhoneOrder;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class PhoneOrderRecordScenarioHistoryDto
{
    public int Id { get; set; }

    public int RecordId { get; set; }

    public DialogueScenarios Scenario { get; set; }

    public ModifyType ModifyType { get; set; }
    
    public int UpdatedBy { get; set; } 
    
    public string UserName { get; set; }

    public DateTimeOffset CreatedDate { get; set; }
}