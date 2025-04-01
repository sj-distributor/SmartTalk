using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeCommandHandler : ICommandHandler<UpdateAiSpeechAssistantKnowledgeCommand, UpdateAiSpeechAssistantKnowledgeResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public UpdateAiSpeechAssistantKnowledgeCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<UpdateAiSpeechAssistantKnowledgeResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantKnowledgeCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.UpdateAiSpeechAssistantKnowledgeAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}