using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneItemDto
{
    public int Id { get; set; }

    public int SceneId { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneItemType Type { get; set; }

    public string Content { get; set; }

    public string FileName { get; set; }

    public KnowledgeSceneStatus SceneStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
