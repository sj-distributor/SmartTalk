using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantDetailCommandHandler : ICommandHandler<UpdateAiSpeechAssistantDetailCommand, UpdateAiSpeechAssistantDetailResponse>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public UpdateAiSpeechAssistantDetailCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task<UpdateAiSpeechAssistantDetailResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantDetailCommand> context, CancellationToken cancellationToken)
    {
        return await _aiSpeechAssistantService.UpdateAiSpeechAssistantDetailAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}