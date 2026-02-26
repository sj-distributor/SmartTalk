using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiKids;
using SmartTalk.Messages.Commands.AiKids;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiKids;

public class AiKidRealtimeCommandHandler : ICommandHandler<AiKidRealtimeCommand>
{
    private readonly IAiKidRealtimeServiceV2 _aiKidRealtimeService;

    public AiKidRealtimeCommandHandler(IAiKidRealtimeServiceV2 aiKidRealtimeService)
    {
        _aiKidRealtimeService = aiKidRealtimeService;
    }

    public async Task Handle(IReceiveContext<AiKidRealtimeCommand> context, CancellationToken cancellationToken)
    {
        await _aiKidRealtimeService.RealtimeAiConnectAsync(context.Message, cancellationToken);
    }
}