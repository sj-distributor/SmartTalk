using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantKnowledgeSceneItemDto
{
    public int Id { get; set; }

    public int SceneId { get; set; }

    public string SceneName { get; set; }

    public KnowledgeSceneStatus SceneStatus { get; set; }

    public string Name { get; set; }

    public int Type { get; set; }

    public string Content { get; set; }

    public string FileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
