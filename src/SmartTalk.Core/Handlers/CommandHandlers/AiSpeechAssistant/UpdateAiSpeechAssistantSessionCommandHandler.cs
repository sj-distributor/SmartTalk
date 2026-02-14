using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantSessionCommandHandler : ICommandHandler<UpdateAiSpeechAssistantSessionCommand, UpdateAiSpeechAssistantSessionResponse>
{
    private readonly IAiSpeechAssistantService _assistantService;

    public UpdateAiSpeechAssistantSessionCommandHandler(IAiSpeechAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    public async Task<UpdateAiSpeechAssistantSessionResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantSessionCommand> context, CancellationToken cancellationToken)
    {
        return await _assistantService.UpdateAiSpeechAssistantSessionAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}