using Mediator.Net.Context;
using Mediator.Net.Contracts;
using SmartTalk.Core.Services.AiResourceSync;
using SmartTalk.Core.Services.Sale;
using SmartTalk.Messages.Commands.AiResourceSync;

namespace SmartTalk.Core.Handlers.CommandHandlers.AiResourceSync;

public class AiResourceSyncCommandHandler : ICommandHandler<AiResourceSyncCommand, AiResourceSyncResponse>
{
    private readonly IAiResourceSyncService _aiResourceSyncService ;

    public AiResourceSyncCommandHandler(IAiResourceSyncService aiResourceSyncService)
    {
        _aiResourceSyncService = aiResourceSyncService;
    }

    public async Task<AiResourceSyncResponse> Handle(IReceiveContext<AiResourceSyncCommand> context, CancellationToken cancellationToken)
    {
        var @event = await _aiResourceSyncService.SyncCrmSalesAutoCreateAsync(context.Message, cancellationToken).ConfigureAwait(false);
        
        await context.PublishAsync(@event, cancellationToken).ConfigureAwait(false);

        return new AiResourceSyncResponse
        {
            Data = new AiResourceSyncResponseData
            {
              TotalCount = @event.TotalCount
            }
        };
    }
}
