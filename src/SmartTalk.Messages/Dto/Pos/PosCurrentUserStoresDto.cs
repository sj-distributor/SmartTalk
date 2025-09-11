using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.Pos;

public class PosCurrentUserStoresDto
{
    public int Id { get; set; }

    public CompanyStoreDto Store { get; set; }

    public AgentAssistantsDto AgentAssistantss { get; set; }
}

public class AgentAssistantsDto
{
    public int AgentId { get; set; }
    
    public List<AiSpeechAssistantDto> AiSpeechAssistants { get; set; }
}