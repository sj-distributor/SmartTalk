using SmartTalk.Messages.Enums.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.AiSpeechAssistant;

public class AiSpeechAssistantDynamicConfigDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public AiSpeechAssistantDynamicConfigLevel Level { get; set; }

    public int? ParentId { get; set; }

    public bool Status { get; set; }

    public List<AiSpeechAssistantDynamicConfigDto> Children { get; set; } = [];
}
