using Mediator.Net.Contracts;
using SmartTalk.Messages.Dto.Agent;
using SmartTalk.Messages.Enums.AiSpeechAssistant;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.Agent;

public class DeleteAgentCommand : ICommand
{
    public int AgentId { get; set; }
}

public class DeleteAgentResponse : SmartTalkResponse<AgentDto>
{
}