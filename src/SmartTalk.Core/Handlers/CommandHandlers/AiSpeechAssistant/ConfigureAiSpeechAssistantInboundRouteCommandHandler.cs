using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ConfigureAiSpeechAssistantInboundRouteCommandHandler : ICommandHandler<ConfigureAiSpeechAssistantInboundRouteCommand>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public ConfigureAiSpeechAssistantInboundRouteCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<ConfigureAiSpeechAssistantInboundRouteCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantService.ConfigureAiSpeechAssistantInboundRouteAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}