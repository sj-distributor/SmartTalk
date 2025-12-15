namespace SmartTalk.Messages.Dto.PhoneOrder;

public class SimplePhoneOrderRecordDto
{
    public int Id { get; set; }
    
    public int AgentId { get; set; }

    public int? AssistantId { get; set; }
}