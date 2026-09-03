namespace SmartTalk.Messages.Dto.SalesAutoCreate;

public class AiResourceSyncCreateAssistantChangeDto
{
    public int AssistantId { get; set; }

    public int? StoreId { get; set; }

    public int? AgentId { get; set; }

    public string AssistantName { get; set; }

    public string PreviousAssistantName { get; set; }

    public int? PreviousStoreId { get; set; }
}