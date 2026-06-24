namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeCapabilityDto
{
    public int KnowledgeId { get; set; }

    public int AssistantId { get; set; }

    public int AgentId { get; set; }

    public string KnowledgeName { get; set; }

    public string AssistantName { get; set; }

    public bool HifoodDataEnabled { get; set; }

    public bool RepeatOrderEnabled { get; set; }

    public bool OrderPushHifoodEnabled { get; set; }
}

public class UpdateAiSpeechAssistantKnowledgeCapabilityDto
{
    public int KnowledgeId { get; set; }

    public int? AssistantId { get; set; }

    public bool? HifoodDataEnabled { get; set; }

    public bool? RepeatOrderEnabled { get; set; }

    public bool? OrderPushHifoodEnabled { get; set; }
}
