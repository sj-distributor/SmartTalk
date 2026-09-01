using SmartTalk.Messages.Enums.KnowledgeScenario;

namespace SmartTalk.Messages.Dto.KnowledgeScenario;

public class KnowledgeSceneLanguageMappingDto
{
    public int? MappingId { get; set; }

    public AutoAddLanguage Language { get; set; }

    public int? SceneId { get; set; }

    public string SceneName { get; set; }
}

public class SaveKnowledgeSceneLanguageMappingItemDto
{
    public AutoAddLanguage Language { get; set; }

    public int SceneId { get; set; }
}

public class KnowledgeSceneAutoAddLanguageMappingsDto
{
    public int CompanyId { get; set; }

    public List<KnowledgeSceneLanguageMappingDto> Mappings { get; set; } = new();
}
