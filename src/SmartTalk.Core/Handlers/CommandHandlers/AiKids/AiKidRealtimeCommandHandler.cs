using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiKids;
using SmartTalk.Core.Services.RealtimeAi.Services;
using SmartTalk.Messages.Commands.AiKids;

namespace SmartTalk.Core.Handlers.CommandHandlers.RealtimeAi;

public class AiKidRealtimeCommandHandler : ICommandHandler<AiKidRealtimeCommand>
{
    private readonly IAiKidRealtimeService _aiKidRealtimeService;

    public AiKidRealtimeCommandHandler(IAiKidRealtimeService aiKidRealtimeService)
    {
        _aiKidRealtimeService = aiKidRealtimeService;
    }

    public async Task Handle(IReceiveContext<AiKidRealtimeCommand> context, CancellationToken cancellationToken)
    {
        await _aiKidRealtimeService.RealtimeAiConnectAsync(context.Message, cancellationToken);
    }
}