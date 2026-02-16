using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiSpeechAssistant;
using SmartTalk.Core.Services.AiSpeechAssistantConnect;
using SmartTalk.Core.Settings.AiSpeechAssistant;
using SmartTalk.Messages.Commands.AiSpeechAssistant;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiSpeechAssistant;

public class ConnectAiSpeechAssistantCommandHandler : ICommandHandler<ConnectAiSpeechAssistantCommand>
{
    private readonly IAiSpeechAssistantService _v1Service;
    private readonly IAiSpeechAssistantConnectService _v2Service;
    private readonly AiSpeechAssistantEngineSettings _engineSettings;

    public ConnectAiSpeechAssistantCommandHandler(
        IAiSpeechAssistantService v1Service,
        IAiSpeechAssistantConnectService v2Service,
        AiSpeechAssistantEngineSettings engineSettings)
    {
        _v1Service = v1Service;
        _v2Service = v2Service;
        _engineSettings = engineSettings;
    }

    public async Task Handle(IReceiveContext<ConnectAiSpeechAssistantCommand> context, CancellationToken cancellationToken)
    {
        var @event = _engineSettings.UseV2Engine
            ? await _v2Service.ConnectAsync(context.Message, cancellationToken).ConfigureAwait(false)
            : await _v1Service.ConnectAiSpeechAssistantAsync(context.Message, cancellationToken).ConfigureAwait(false);

        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);
    }
}
