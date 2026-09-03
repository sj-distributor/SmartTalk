using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneUpdatedSnapshotDto
{
    public int SceneId { get; set; }

    public int FolderId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Version { get; set; }

    public KnowledgeSceneStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public List<KnowledgeSceneUpdatedSnapshotItem> SceneItems { get; set; } = [];
}

public class KnowledgeSceneUpdatedSnapshotItem
{
    public int SceneItemId { get; set; }

    public string Name { get; set; }

    public KnowledgeSceneItemType Type { get; set; }

    public string Content { get; set; }

    public string FileName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}