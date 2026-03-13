using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeDetailCommand : ICommand
{
    public int KnowledgeId { get; set; }
    
    public string KnowledgeName { get; set; }
    
    public AiSpeechAssistantKonwledgeFormatType FormatType { get; set; }
    
    public string Content { get; set; }
}

public class AddAiSpeechAssistantKnowledgeDetailResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDetailDto>
{
}