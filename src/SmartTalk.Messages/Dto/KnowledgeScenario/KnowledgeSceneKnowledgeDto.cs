using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneKnowledgeDto
{
    public int Id { get; set; }

    public int SceneId { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneKnowledgeType Type { get; set; }

    public string Content { get; set; }

    public string FileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
