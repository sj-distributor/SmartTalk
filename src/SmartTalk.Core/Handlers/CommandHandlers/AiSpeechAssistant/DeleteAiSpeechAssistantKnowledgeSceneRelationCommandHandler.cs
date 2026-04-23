using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class DeleteAiSpeechAssistantKnowledgeSceneRelationCommandHandler : ICommandHandler<DeleteAiSpeechAssistantKnowledgeSceneRelationCommand, DeleteAiSpeechAssistantKnowledgeSceneRelationResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public DeleteAiSpeechAssistantKnowledgeSceneRelationCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<DeleteAiSpeechAssistantKnowledgeSceneRelationResponse> Handle(IReceiveContext<DeleteAiSpeechAssistantKnowledgeSceneRelationCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.DeleteAiSpeechAssistantKnowledgeSceneRelationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
