using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class DeleteAiSpeechAssistantKnowledgeDetailCommandHandler : ICommandHandler<DeleteAiSpeechAssistantKnowledgeDetailCommand,DeleteAiSpeechAssistantKnowledgeDetailResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public DeleteAiSpeechAssistantKnowledgeDetailCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<DeleteAiSpeechAssistantKnowledgeDetailResponse> Handle(IReceiveContext<DeleteAiSpeechAssistantKnowledgeDetailCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantService.DeleteAiSpeechAssistantKnowledgeDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
        
        return new DeleteAiSpeechAssistantKnowledgeDetailResponse();
    }
}