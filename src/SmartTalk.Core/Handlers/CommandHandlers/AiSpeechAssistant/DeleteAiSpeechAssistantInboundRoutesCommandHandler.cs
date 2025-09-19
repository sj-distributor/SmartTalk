using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class DeleteAiSpeechAssistantInboundRoutesCommandHandler : ICommandHandler<DeleteAiSpeechAssistantInboundRoutesCommand, DeleteAiSpeechAssistantInboundRoutesResponse>
{
    private readonly IAiSpeechAssistantService _aiiSpeechAssistantService;

    public DeleteAiSpeechAssistantInboundRoutesCommandHandler(IAiSpeechAssistantService aiiSpeechAssistantService)
    {
        _aiiSpeechAssistantService = aiiSpeechAssistantService;
    }

    public async Task<DeleteAiSpeechAssistantInboundRoutesResponse> Handle(IReceiveContext<DeleteAiSpeechAssistantInboundRoutesCommand> context, CancellationToken cancellationToken)
    {
        return await _aiiSpeechAssistantService.DeleteAiSpeechAssistantInboundRoutesAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}