using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantCommandHandler : ICommandHandler<UpdateAiSpeechAssistantCommand, UpdateAiSpeechAssistantResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public UpdateAiSpeechAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<UpdateAiSpeechAssistantResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.UpdateAiSpeechAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}