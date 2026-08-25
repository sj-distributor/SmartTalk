namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneFolderDto
{
    public int Id { get; set; }

    public string Name { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }
}
