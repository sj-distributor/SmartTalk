using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeDetailCommandHandler : ICommandHandler<UpdateAiSpeechAssistantKnowledgeDetailCommand, UpdateAiSpeechAssistantKnowledgeDetailResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public UpdateAiSpeechAssistantKnowledgeDetailCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<UpdateAiSpeechAssistantKnowledgeDetailResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantKnowledgeDetailCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.UpdateAiSpeechAssistantKnowledgeDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}