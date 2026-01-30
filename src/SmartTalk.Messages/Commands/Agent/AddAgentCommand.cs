using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Requests.Pos;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Agent;

public class AddAgentCommand : HasServiceProviderId, ICommand
{
    public int StoreId { get; set; }
    
    public string Name { get; set; }
    
    public string Brief { get; set; }
    
    public bool IsReceivingCall { get; set; }
    
    public string Voice { get; set; }
    
    public int WaitInterval { get; set; }

    public bool IsTransferHuman { get; set; } = false;
    
    public string TransferCallNumber { get; set; }

    public AiSpeechAssistantChannel Channel { get; set; } = AiSpeechAssistantChannel.PhoneChat;
}

public class AddAgentResponse : SmartTalkResponse<AgentDto>
{
}