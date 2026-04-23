using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneDto
{
    public int Id { get; set; }
    
    public int FolderId { get; set; }
    
    public string Name { get; set; }

    public string Description { get; set; }

    public KnowledgeSceneStatus Status { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    
    public DateTimeOffset? UpdatedAt { get; set; }
}