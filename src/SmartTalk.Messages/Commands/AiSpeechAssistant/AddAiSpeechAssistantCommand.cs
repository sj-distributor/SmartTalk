using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Enums.RealtimeAi;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantCommand : HasServiceProviderId, ICommand
{
    public int? AgentId { get; set; }
    
    public string AssistantName { get; set; }
    
    public string Greetings { get; set; }
    
    public string Json { get; set; }
    
    public List<AiSpeechAssistantChannel> Channels { get; set; }
    
    public Guid? Uuid { get; set; }
    
    public bool IsDisplay { get; set; } = true;
    
    public AiSpeechAssistantVoiceType? VoiceType { get; set; }
    
    public AgentType AgentType { get; set; } = AgentType.Restaurant;

    public AgentSourceSystem SourceSystem { get; set; } = AgentSourceSystem.Self;
    
    public string ModelVoice { get; set; }
    
    public string ModelUrl { get; set; }
    
    public string ModelName { get; set; }
    
    public string ModelLanguage { get; set; }
    
    public AiSpeechAssistantMediaType? MediaType { get; set; }

    public RealtimeAiProvider ModelProvider { get; set; } = RealtimeAiProvider.OpenAi;
    
    public int? StoreId { get; set; }
}

public class AddAiSpeechAssistantResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}