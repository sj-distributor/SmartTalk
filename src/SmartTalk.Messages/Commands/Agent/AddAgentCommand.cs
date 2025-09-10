using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Agent;

public class AddAgentCommand : ICommand
{
    public string Name { get; set; }
    
    public string Brief { get; set; }
    
    public bool IsReceivingCall { get; set; }
    
    public int ServiceProviderId { get; set; }

    public AiSpeechAssistantChannel Channel { get; set; } = AiSpeechAssistantChannel.PhoneChat;
}

public class AddAgentResponse : SmartTalkResponse<AgentDto>
{
}