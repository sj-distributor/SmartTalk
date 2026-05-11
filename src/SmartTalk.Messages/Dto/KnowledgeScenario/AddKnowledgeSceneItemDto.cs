using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class AddKnowledgeSceneItemDto
{
    public string Name { get; set; }

    public KnowledgeSceneItemType Type { get; set; } = KnowledgeSceneItemType.Text;

    public string Content { get; set; }

    public string FileName { get; set; }
}