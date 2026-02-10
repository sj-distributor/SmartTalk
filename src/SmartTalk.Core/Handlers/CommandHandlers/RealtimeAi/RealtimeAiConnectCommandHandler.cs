using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Messages.Commands.RealtimeAi;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAi;

public class RealtimeAiConnectCommandHandler : ICommandHandler<RealtimeAiConnectCommand>
{
    private readonly IAiSpeechAssistantRealtimeService _aiSpeechAssistantRealtimeService;

    public RealtimeAiConnectCommandHandler(IAiSpeechAssistantRealtimeService aiSpeechAssistantRealtimeService)
    {
        _aiSpeechAssistantRealtimeService = aiSpeechAssistantRealtimeService;
    }

    public async Task Handle(IReceiveContext<RealtimeAiConnectCommand> context, CancellationToken cancellationToken)
    {
        await _aiSpeechAssistantRealtimeService.RealtimeAiConnectAsync(context.Message, cancellationToken);
    }
}