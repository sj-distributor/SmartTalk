using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Messages.Commands.PhoneCall;
using SmartTalk.Core.Services.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ConnectAiSpeechAssistantCommandHandler : ICommandHandler<ConnectAiSpeechAssistantCommand>
{
    private readonly IAiSpeechAssistantService _aiSpeechAssistantService;

    public ConnectAiSpeechAssistantCommandHandler(IAiSpeechAssistantService aiSpeechAssistantService)
    {
        _aiSpeechAssistantService = aiSpeechAssistantService;
    }

    public async Task Handle(IReceiveContext<ConnectAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _aiSpeechAssistantService.ConnectAiSpeechAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
    }
}