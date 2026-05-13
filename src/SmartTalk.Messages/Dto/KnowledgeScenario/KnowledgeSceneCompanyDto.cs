namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneCompanyDto
{
    public int Id { get; set; }

    public int SceneId { get; set; }

    public int CompanyId { get; set; }

    public bool IsApplied { get; set; }

    public DateTimeOffset AuthorizedAt { get; set; }
}
