using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantDynamicConfigCommand : ICommand
{
    public int Id { get; set; }
    
    public bool Status { get; set; }
    
    public List<int> CompanyIds { get; set; } = [];
}

public class UpdateAiSpeechAssistantDynamicConfigResponse : SmartTalkResponse<AiSpeechAssistantDynamicConfigDto>
{
}
