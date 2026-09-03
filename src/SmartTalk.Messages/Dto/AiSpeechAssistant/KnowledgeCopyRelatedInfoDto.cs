using SmartTalk.Messages.Enums.Agent;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class KnowledgeCopyRelatedInfoDto
{
    public int AssistantId { get; set; }
    
    public string AssiatantName { get; set; }
    
    public string StoreName { get; set; }
    
    public int KnowledgeId { get; set; }
    
    public string AiAgentName { get; set; }

    public AgentSourceSystem SourceSystem { get; set; }
    
    public List<AiSpeechAssistantKnowledgeSceneRelationDto> SceneRelations { get; set; } = new();
}
