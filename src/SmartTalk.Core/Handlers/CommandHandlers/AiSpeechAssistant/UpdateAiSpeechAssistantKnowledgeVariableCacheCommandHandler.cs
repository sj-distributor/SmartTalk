using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeVariableCacheCommandHandler : ICommandHandler<UpdateAiSpeechAssistantKnowledgeVariableCacheCommand>
{
    private readonly IAiSpeechAssistantService _aiiSpeechAssistantService;

    public UpdateAiSpeechAssistantKnowledgeVariableCacheCommandHandler(IAiSpeechAssistantService aiiSpeechAssistantService)
    {
        _aiiSpeechAssistantService = aiiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<UpdateAiSpeechAssistantKnowledgeVariableCacheCommand> context, CancellationToken cancellationToken)
    {
        await _aiiSpeechAssistantService.UpdateAiSpeechAssistantKnowledgeVariableCacheAsync(context.Message, cancellationToken);
    }
}