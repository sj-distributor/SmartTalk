using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;
namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantNumberCommandHandler : ICommandHandler<UpdateAiSpeechAssistantNumberCommand, UpdateAiSpeechAssistantNumberResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public UpdateAiSpeechAssistantNumberCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<UpdateAiSpeechAssistantNumberResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantNumberCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.UpdateAiSpeechAssistantNumberAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}