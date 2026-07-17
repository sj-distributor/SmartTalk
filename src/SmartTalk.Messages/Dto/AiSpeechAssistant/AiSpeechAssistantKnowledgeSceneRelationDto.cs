using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeSceneRelationDto
{
    public int Id { get; set; }

    public int KnowledgeId { get; set; }

    public int SceneId { get; set; }

    public int FolderId { get; set; }

    public string SceneName { get; set; }

    public string SceneDescription { get; set; }

    public KnowledgeSceneStatus SceneStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
