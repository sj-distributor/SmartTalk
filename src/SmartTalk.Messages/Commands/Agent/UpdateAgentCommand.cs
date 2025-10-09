using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Agent;

public class UpdateAgentCommand : ICommand
{
    public int AgentId { get; set; }
    
    public string Name { get; set; }
    
    public string Brief { get; set; }
    
    public bool IsReceivingCall { get; set; }
    
    public int ServiceProviderId { get; set; }
    
    public AiSpeechAssistantChannel Channel { get; set; }
}

public class UpdateAgentResponse : SmartTalkResponse<AgentDto>
{
}