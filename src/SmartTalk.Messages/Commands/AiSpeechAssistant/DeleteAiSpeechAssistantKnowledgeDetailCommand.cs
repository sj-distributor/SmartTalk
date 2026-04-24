using Mediator.Net.Contracts;
using SmartTalk.Messages.Responses;

namespace SmartTalk.Messages.Commands.AiSpeechAssistant;

public class DeleteAiSpeechAssistantKnowledgeDetailCommand : ICommand
{
    public int DetailId { get; set; }
}

public class DeleteAiSpeechAssistantKnowledgeDetailResponse : SmartTalkResponse
{
}