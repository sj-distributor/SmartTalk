using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class AddAiSpeechAssistantKnowledgeSceneRelationCommandHandler : ICommandHandler<AddAiSpeechAssistantKnowledgeSceneRelationCommand, AddAiSpeechAssistantKnowledgeSceneRelationResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public AddAiSpeechAssistantKnowledgeSceneRelationCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<AddAiSpeechAssistantKnowledgeSceneRelationResponse> Handle(IReceiveContext<AddAiSpeechAssistantKnowledgeSceneRelationCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.AddAiSpeechAssistantKnowledgeSceneRelationAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
