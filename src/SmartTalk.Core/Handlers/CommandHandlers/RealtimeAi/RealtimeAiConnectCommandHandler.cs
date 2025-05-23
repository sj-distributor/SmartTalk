using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Messages.Commands.RealtimeAi;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAi;

public class RealtimeAiConnectCommandHandler : ICommandHandler<RealtimeAiConnectCommand>
{
    private readonly IRealtimeAiService _realtimeAiService;

    public RealtimeAiConnectCommandHandler(IRealtimeAiService realtimeAiService)
    {
        _realtimeAiService = realtimeAiService;
    }

    public async Task Handle(IReceiveContext<RealtimeAiConnectCommand> context, CancellationToken cancellationToken)
    {
        await _realtimeAiService.RealtimeAiConnectAsync(context.Message, cancellationToken);
    }
}