using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiResourceSync;

public class SchedulingAiResourceSyncCommandHandler : ICommandHandler<SchedulingAiResourceSyncCommand>
{
    private readonly IAiResourceSyncProcessJobService _aiResourceSyncProcessJobService ;
    
    public SchedulingAiResourceSyncCommandHandler(IAiResourceSyncProcessJobService aiResourceSyncProcessJobService)
    {
        _aiResourceSyncProcessJobService = aiResourceSyncProcessJobService;
    }

    public async Task Handle(IReceiveContext<SchedulingAiResourceSyncCommand> context, CancellationToken cancellationToken)
    {
        await _aiResourceSyncProcessJobService.AiResourceSyncAsync(context.Message, cancellationToken).ConfigureAwait(false);
    }
}
