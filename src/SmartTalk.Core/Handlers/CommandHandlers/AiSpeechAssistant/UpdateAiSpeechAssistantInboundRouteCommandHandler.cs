using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class UpdateAiSpeechAssistantInboundRouteCommandHandler : ICommandHandler<UpdateAiSpeechAssistantInboundRouteCommand, UpdateAiSpeechAssistantInboundRouteResponse>
{
    private readonly IAiSpeechAssistantService _aiiSpeechAssistantService;

    public UpdateAiSpeechAssistantInboundRouteCommandHandler(IAiSpeechAssistantService aiiSpeechAssistantService)
    {
        _aiiSpeechAssistantService = aiiSpeechAssistantService;
    }

    public async Task<UpdateAiSpeechAssistantInboundRouteResponse> Handle(IReceiveContext<UpdateAiSpeechAssistantInboundRouteCommand> context, CancellationToken cancellationToken)
    {
        return await _aiiSpeechAssistantService.UpdateAiSpeechAssistantInboundRouteAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}