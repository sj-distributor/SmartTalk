using SmartTalk.Messages.Dto.AiSpeechAssistant;

namespace SmartTalk.Messages.Dto.Pos;

public class PosAgentDto
{
    public int Id { get; set; }

    public int StoreId { get; set; }
    
    public string StoreName { get; set; }

    public List<AgentAssistantsDto> AgentAssistantss { get; set; }
}

public class AgentAssistantsDto
{
    public int AgentId { get; set; }
    
    public List<AiSpeechAssistantDto> AiSpeechAssistants { get; set; }
}