namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneDetailDto : KnowledgeSceneDto
{
    public List<KnowledgeSceneKnowledgeDto> Knowledges { get; set; } = new();
}
