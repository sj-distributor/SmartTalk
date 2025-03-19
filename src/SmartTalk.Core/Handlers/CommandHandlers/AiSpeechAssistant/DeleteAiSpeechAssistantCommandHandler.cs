using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class DeleteAiSpeechAssistantCommandHandler : ICommandHandler<DeleteAiSpeechAssistantCommand, DeleteAiSpeechAssistantResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public DeleteAiSpeechAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<DeleteAiSpeechAssistantResponse> Handle(IReceiveContext<DeleteAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.DeleteAiSpeechAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}