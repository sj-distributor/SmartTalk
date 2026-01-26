using SmartTalk.Messages.Enums.PhoneOrder;
using TaskStatus = SmartTalk.Messages.Enums.PhoneOrder.TaskStatus;

namespace SmartTalk.Messages.Dto.PhoneOrder;

public class WaitingProcessingEventsDto
{
    public int Id { get; set; }
    
    public int RecordId { get; set; }
    
    public int AgentId { get; set; }

    public TaskType TaskType { get; set; }

    public TaskStatus TaskStatus { get; set; }
    
    public DialogueScenarios? Scenario { get; set; }

    public string TaskSource { get; set; }

    public string Url { get; set; }

    public string SessionId { get; set; }
    
    public DateTimeOffset CreatedDate { get; set; }
    
    public string LastModifiedByName { get; set; }
}