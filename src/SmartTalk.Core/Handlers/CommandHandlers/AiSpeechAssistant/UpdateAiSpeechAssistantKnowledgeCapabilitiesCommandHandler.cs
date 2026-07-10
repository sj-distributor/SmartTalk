using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantKnowledgeCapabilitiesCommandHandler
    : ICommandHandler<UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand, UpdateAiSpeechAssistantKnowledgeCapabilitiesResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public UpdateAiSpeechAssistantKnowledgeCapabilitiesCommandHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<UpdateAiSpeechAssistantKnowledgeCapabilitiesResponse> Handle(
        IReceiveContext<UpdateAiSpeechAssistantKnowledgeCapabilitiesCommand> context,
        CancellationToken cancellationToken)
    {
        return await _assistantService
            .UpdateAiSpeechAssistantKnowledgeCapabilitiesAsync(context.Message, cancellationToken)
            .ConfigureAwait(false);
    }
}
