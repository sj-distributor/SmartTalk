using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeCommand : ICommand
{
    public string Brief { get; set; }
    
    public int? KnowledgeId { get; set; }
    
    public int? AssistantId { get; set; }
    
    public string Greetings { get; set; }
    
    public string ModelVoice { get; set; }

    public AiSpeechAssistantPremiseDto? Premise { get; set; }
    
    public AiSpeechAssistantVoiceType? VoiceType { get; set; }
    
    public AiSpeechAssistantMediaType? MediaType { get; set; }
}

public class UpdateAiSpeechAssistantKnowledgeResponse : SmartTalkResponse<AiSpeechAssistantKnowledgeDto>
{
}