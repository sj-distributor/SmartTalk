using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeDetailCommand : ICommand
{
    public int DetailId { get; set; }
    
    public string KnowledgeName { get; set; }
    
    public AiSpeechAssistantKonwledgeFormatType FormatType { get; set; }
}

public class UpdateAiSpeechAssistantKnowledgeDetailResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDetailDto>
{
}