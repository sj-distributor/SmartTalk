using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Dto.AiSpeechAssistant;
using SmartTalk.Messages.Enums.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class AddAiSpeechAssistantCommand : ICommand
{
    public string AssistantName { get; set; }
    
    public string Greetings { get; set; }
    
    public string Json { get; set; }
    
    public List<AiSpeechAssistantChannel> Channels { get; set; }
    
    public Guid? Uuid { get; set; }
    
    public bool IsDisplay { get; set; } = true;
    
    public AiKidVoiceType? VoiceType { get; set; }
    
    public AgentType AgentType { get; set; } = AgentType.Restaurant;

    public AgentSourceSystem SourceSystem { get; set; } = AgentSourceSystem.Self;
}

public class AddAiSpeechAssistantResponse : SmartTalkResponse<AiSpeechAssistantDto>
{
}